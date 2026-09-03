using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using x402dev.Services;
using x402dev.Shared.Models;

namespace x402dev.Server.Controllers
{
    [ApiController]
    [Route("api/x402-apis")]
    public class X402ApisController : ControllerBase
    {
        private readonly X402ApiService x402ApiService;
        private readonly IConfiguration configuration;

        public X402ApisController(X402ApiService x402ApiService, IConfiguration configuration)
        {
            this.x402ApiService = x402ApiService;
            this.configuration = configuration;
        }

        /// <summary>
        /// Register a new x402 API endpoint by URL.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AddX402ApiRequest request)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (api, error) = await x402ApiService.AddX402ApiAsync(request?.Url ?? string.Empty, clientIp);

            return api == null
                ? BadRequest(new { error })
                : Created($"/x402-apis/detail?url={Uri.EscapeDataString(api.Url)}", new { api.Url, api.Domain });
        }

        /// <summary>
        /// Delete x402 API entries by full url or by domain (all entries for the domain).
        /// Admin only: requires the secret configured under AdminApi:Secret.
        /// The API is disabled when no secret is configured.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> Delete([FromBody] AdminX402ApiDeleteRequest request)
        {
            var configuredSecret = configuration["AdminApi:Secret"];

            if (string.IsNullOrEmpty(configuredSecret))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Admin API is not configured." });
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Secret) || !SecretMatches(configuredSecret, request.Secret))
            {
                return Unauthorized(new { error = "Invalid secret." });
            }

            var hasUrl = !string.IsNullOrWhiteSpace(request.Url);
            var hasDomain = !string.IsNullOrWhiteSpace(request.Domain);

            if (hasUrl == hasDomain)
            {
                return BadRequest(new { error = "Provide exactly one of url or domain." });
            }

            var deleted = await x402ApiService.DeleteX402ApisAsync(hasUrl ? request.Url : null, hasDomain ? request.Domain : null);

            return deleted == 0
                ? NotFound(new { error = "No matching entries found." })
                : Ok(new { deleted });
        }

        /// <summary>
        /// Delete all x402 API entries that have never had a successful check,
        /// or whose last successful check is older than 7 days.
        /// Admin only: requires the secret configured under AdminApi:Secret.
        /// </summary>
        [HttpDelete("cleanup")]
        public async Task<IActionResult> Cleanup([FromBody] AdminX402ApiCleanupRequest request)
        {
            var configuredSecret = configuration["AdminApi:Secret"];

            if (string.IsNullOrEmpty(configuredSecret))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Admin API is not configured." });
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Secret) || !SecretMatches(configuredSecret, request.Secret))
            {
                return Unauthorized(new { error = "Invalid secret." });
            }

            var deleted = await x402ApiService.CleanupStaleX402ApisAsync();

            return Ok(new { deleted });
        }

        private static bool SecretMatches(string expected, string provided)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided));
        }
    }

    public record AdminX402ApiDeleteRequest
    {
        public string? Secret { get; set; }
        public string? Url { get; set; }
        public string? Domain { get; set; }
    }

    public record AdminX402ApiCleanupRequest
    {
        public string? Secret { get; set; }
    }
}
