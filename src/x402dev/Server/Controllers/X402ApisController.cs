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

        public X402ApisController(X402ApiService x402ApiService)
        {
            this.x402ApiService = x402ApiService;
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
    }
}
