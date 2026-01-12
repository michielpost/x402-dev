using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using x402;
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


    [HttpGet]
    [Route("version")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public string Version()
    {
        return "2";
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

            var existingHeaderValue = Request.Headers[X402HandlerV2.PaymentHeaderV2].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(existingHeaderValue))
            {
                proxyRequest.Headers.Add(X402HandlerV2.PaymentHeaderV2, existingHeaderValue);
            }
            else if (!string.IsNullOrWhiteSpace(request.PaymentHeader))
            {
                proxyRequest.Headers.Add(X402HandlerV2.PaymentHeaderV2, request.PaymentHeader);
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

    [HttpPost]
    [Route("proxy-x402")]
    [EnableRateLimiting("proxy-3sec")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ProxyPassThrough([FromBody] ProxyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            return BadRequest("Invalid or missing URL.");
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            var proxyRequest = new HttpRequestMessage(HttpMethod.Get, request.Url);

            // Add/override the payment header if present
            var existingHeaderValue = Request.Headers[X402HandlerV2.PaymentHeaderV2].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(existingHeaderValue))
            {
                proxyRequest.Headers.Remove(X402HandlerV2.PaymentHeaderV2);
                proxyRequest.Headers.Add(X402HandlerV2.PaymentHeaderV2, existingHeaderValue);
            }
            else if (!string.IsNullOrWhiteSpace(request.PaymentHeader))
            {
                proxyRequest.Headers.Remove(X402HandlerV2.PaymentHeaderV2);
                proxyRequest.Headers.Add(X402HandlerV2.PaymentHeaderV2, request.PaymentHeader);
            }

            var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead);

            // Copy status code
            Response.StatusCode = (int)response.StatusCode;

            // Copy headers
            foreach (var header in response.Headers)
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in response.Content.Headers)
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
            // Remove headers that are forbidden to set
            Response.Headers.Remove("transfer-encoding");

            Response.Headers.Remove("Access-Control-Expose-Headers");
            Response.Headers.Append("Access-Control-Expose-Headers", X402HandlerV2.PaymentRequiredHeader);

            // Copy content
            var content = await response.Content.ReadAsStreamAsync();
            await content.CopyToAsync(Response.Body);
            return new EmptyResult();
        }
        catch (HttpRequestException ex) { return StatusCode(502, $"Target unreachable: {ex.Message}"); }
        catch (TaskCanceledException) { return StatusCode(504, "Request timed out."); }
        catch (Exception ex) { return StatusCode(500, $"Internal error: {ex.Message}"); }
    }
}