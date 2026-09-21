using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using x402dev.Services;

namespace x402dev.Server.Mcp
{
    /// <summary>
    /// MCP tools for the x402 API registry: submit new endpoints and search existing ones.
    /// </summary>
    [McpServerToolType]
    public static class X402ApiMcpTools
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Registers a new x402 API endpoint. Only the URL is needed; the endpoint
        /// will be checked in the background and become visible once verified.
        /// </summary>
        [McpServerTool(Name = "add_x402_api")]
        public static string AddX402Api(
            IServiceProvider services,
            [Description("The base URL of the x402 service, e.g. https://example.com/resource")] string url)
        {
            var x402ApiService = services.GetRequiredService<X402ApiService>();
            var clientIp = services.GetService<IHttpContextAccessor>()?.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var (api, error) = x402ApiService.AddX402ApiAsync(url, clientIp).GetAwaiter().GetResult();

            if (api == null)
            {
                return $"Failed to add the x402 API: {error}";
            }

            return $"Added {api.Url}. It will be checked shortly and appear in the registry once verified.";
        }

        /// <summary>
        /// Searches verified x402 services by url, domain, service name, description or payments.
        /// </summary>
        [McpServerTool(Name = "search_x402_apis")]
        public static string SearchX402Apis(
            IServiceProvider services,
            [Description("Free-text search in url, domain, service name and description. Empty returns all verified services.")] string? query = null,
            [Description("Maximum number of results to return (1-100).")] int max = 20)
        {
            if (max < 1) max = 1;
            if (max > 100) max = 100;

            var x402ApiService = services.GetRequiredService<X402ApiService>();
            var apis = x402ApiService.GetCheckedX402ApisAsync().GetAwaiter().GetResult();

            IEnumerable<Database.Models.X402Api> results = apis;

            if (!string.IsNullOrWhiteSpace(query))
            {
                results = results.Where(a =>
                    Contains(a.Url, query) ||
                    Contains(a.Domain, query) ||
                    Contains(a.ServiceName, query) ||
                    Contains(a.Description, query));
            }

            var matches = results
                .OrderBy(a => a.Domain)
                .ThenBy(a => a.Url)
                .Take(max)
                .Select(a => new
                {
                    url = a.Url,
                    domain = a.Domain,
                    serviceName = a.ServiceName,
                    description = a.Description,
                    version = a.Version,
                    payments = ParsePayments(a.PaymentsJson)
                })
                .ToList();

            if (matches.Count == 0)
            {
                return string.IsNullOrWhiteSpace(query)
                    ? "No verified x402 services are registered yet."
                    : $"No verified x402 services match '{query}'.";
            }

            return JsonSerializer.Serialize(matches, JsonOptions);
        }

        private static bool Contains(string? value, string query)
            => !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);

        private static List<object> ParsePayments(string? paymentsJson)
        {
            if (string.IsNullOrWhiteSpace(paymentsJson))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<object>>(paymentsJson, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
