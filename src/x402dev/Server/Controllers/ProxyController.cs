using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Text;
using x402;
using x402dev.Server.Models;
using x402dev.Server.Security;
using x402dev.Services.Security;

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
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            !PublicProxyNetworkPolicy.IsAllowedUri(uri))
        {
            return BadRequest("Invalid or missing URL.");
        }

        var client = _httpClientFactory.CreateClient(PublicProxyNetworkPolicy.ClientName);
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            using var proxyRequest = BuildProxyRequest(request, Request);

            // ResponseHeadersRead keeps the body out of memory until the bounded
            // reader below has confirmed it fits within the size ceiling.
            using var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead);


            // Return payload **only** when the remote server returns 402
            if (response.StatusCode != HttpStatusCode.PaymentRequired && !response.IsSuccessStatusCode)
            {
                var noResult = new ProxyResponse
                {
                    StatusCode = (int)response.StatusCode
                };

                return Ok(noResult);

            }

            var content = await BoundedContentReader.ReadStringAsync(response.Content);
            if (content is null)
            {
                return StatusCode(502, "Target response exceeded the maximum allowed size.");
            }

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
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            !PublicProxyNetworkPolicy.IsAllowedUri(uri))
        {
            return BadRequest("Invalid or missing URL.");
        }

        var client = _httpClientFactory.CreateClient(PublicProxyNetworkPolicy.ClientName);
        client.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            using var proxyRequest = BuildProxyRequest(request, Request);

            using var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead);

            // Copy status code
            Response.StatusCode = (int)response.StatusCode;

            // Copy headers, except the ones a target must never control on this
            // origin (Set-Cookie, hop-by-hop headers, and the CORS policy).
            foreach (var header in response.Headers)
            {
                if (ProxyResponseHeaderPolicy.IsForwarded(header.Key))
                    Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in response.Content.Headers)
            {
                if (ProxyResponseHeaderPolicy.IsForwarded(header.Key))
                    Response.Headers[header.Key] = header.Value.ToArray();
            }

            Response.Headers["Access-Control-Expose-Headers"] = X402HandlerV2.PaymentRequiredHeader;

            // Copy content
            var content = await response.Content.ReadAsStreamAsync();
            await content.CopyToAsync(Response.Body);
            return new EmptyResult();
        }
        catch (HttpRequestException ex) { return StatusCode(502, $"Target unreachable: {ex.Message}"); }
        catch (TaskCanceledException) { return StatusCode(504, "Request timed out."); }
        catch (Exception ex) { return StatusCode(500, $"Internal error: {ex.Message}"); }
    }

    private static HttpRequestMessage BuildProxyRequest(ProxyRequest request, HttpRequest incomingRequest)
    {
        var httpMethod = ParseHttpMethod(request.HttpMethod);
        if (httpMethod == null)
        {
            throw new ArgumentException($"Unsupported HTTP method: {request.HttpMethod}");
        }

        var proxyRequest = new HttpRequestMessage(httpMethod, request.Url);

        // Add/override the payment header if present
        var existingHeaderValue = incomingRequest.Headers[X402HandlerV2.PaymentHeaderV2].FirstOrDefault();
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

        // Forward an optional request body for methods that support one.
        if (!string.IsNullOrWhiteSpace(request.RequestContent) &&
            httpMethod != HttpMethod.Get &&
            httpMethod != HttpMethod.Head)
        {
            proxyRequest.Content = new StringContent(request.RequestContent, Encoding.UTF8, "application/json");
        }

        return proxyRequest;
    }

    private static HttpMethod? ParseHttpMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return HttpMethod.Get;
        }

        var normalized = method.Trim().ToUpperInvariant();
        return normalized switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            "PATCH" => HttpMethod.Patch,
            _ => null
        };
    }
}