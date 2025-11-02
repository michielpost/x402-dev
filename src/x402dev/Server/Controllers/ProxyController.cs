using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using x402dev.Server.Models;

namespace PaymentRequiredProxyApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    [Route("get-x402")]
    [EnableRateLimiting("proxy-3sec")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GetPaymentRequired([FromBody] ProxyRequest request, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            return BadRequest("Invalid or missing URL.");
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var proxyRequest = new HttpRequestMessage(HttpMethod.Get, request.Url);

            if (!string.IsNullOrWhiteSpace(request.PaymentHeader))
            {
                proxyRequest.Headers.Add("X-PAYMENT", request.PaymentHeader);
            }

            var response = await client.SendAsync(proxyRequest);


            // Return payload **only** when the remote server returns 402
            if (response.StatusCode != HttpStatusCode.PaymentRequired && !response.IsSuccessStatusCode)
            {
                var noResult = new ProxyResponse
                {
                    StatusCode = (int)response.StatusCode
                };

                return Ok(noResult);

            }

            var content = await response.Content.ReadAsStringAsync();

            var headers = response.Headers
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            var result = new ProxyResponse
            {
                StatusCode = (int)response.StatusCode,
                Headers = headers,
                Content = content
            };

            return Ok(result);
        }
        catch (HttpRequestException ex) { return StatusCode(502, $"Target unreachable: {ex.Message}"); }
        catch (TaskCanceledException) { return StatusCode(504, "Request timed out."); }
        catch (Exception ex) { return StatusCode(500, $"Internal error: {ex.Message}"); }
    }
}