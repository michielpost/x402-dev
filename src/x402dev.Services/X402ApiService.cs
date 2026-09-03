using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using x402.Core.Models.v2;
using x402dev.Database;
using x402dev.Database.Models;

namespace x402dev.Services
{
    public class X402ApiService(ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<X402ApiService> logger)
    {
        public const string PaymentRequiredHeader = "PAYMENT-REQUIRED";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>Maximum number of endpoints that can be registered per domain.</summary>
        public const int MaxApisPerDomain = 20;

        /// <summary>Maximum number of URLs one client (IP) may add per rolling hour.</summary>
        public const int MaxAddsPerHourPerIp = 10;

        public async Task<List<X402Api>> GetCheckedX402ApisAsync(int max = 500)
        {
            var now = DateTimeOffset.UtcNow;

            var apis = await dbContext.X402Apis
                .Where(x => x.LastCheckDateTime != null)
                .OrderBy(x => x.Domain)
                .ThenBy(x => x.Url)
                .Take(max)
                .ToListAsync();

            return apis.Where(x => IsVisible(x, now)).ToList();
        }

        public Task<X402Api?> GetX402ApiByIdAsync(int id)
        {
            return dbContext.X402Apis.FirstOrDefaultAsync(x => x.Id == id);
        }

        public Task<X402Api?> GetX402ApiByUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Task.FromResult<X402Api?>(null);
            }

            var normalized = url.Trim().ToLowerInvariant();
            return dbContext.X402Apis.FirstOrDefaultAsync(x => x.Url.ToLower() == normalized);
        }

        public async Task<List<X402Api>> GetX402ApisByDomainAsync(string domain, int max = 500)
        {
            var now = DateTimeOffset.UtcNow;

            var apis = await dbContext.X402Apis
                .Where(x => x.LastCheckDateTime != null && x.Domain == domain)
                .OrderBy(x => x.Url)
                .Take(max)
                .ToListAsync();

            return apis.Where(x => IsVisible(x, now)).ToList();
        }

        /// <summary>
        /// APIs that currently have an error or have not been checked yet.
        /// </summary>
        public Task<List<X402Api>> GetProblemX402ApisAsync(int max = 500)
        {
            return dbContext.X402Apis
                .Where(x => x.LastCheckDateTime == null
                    || (x.LastErrorDateTime != null
                        && (!x.LastSuccessDateTime.HasValue || x.LastErrorDateTime > x.LastSuccessDateTime)))
                .OrderBy(x => x.Domain)
                .ThenBy(x => x.Url)
                .Take(max)
                .ToListAsync();
        }

        /// <summary>
        /// APIs with an error are hidden once their last successful check is older than 48 hours.
        /// </summary>
        private static bool IsVisible(X402Api api, DateTimeOffset now)
        {
            var hasError = api.LastErrorDateTime.HasValue
                && (!api.LastSuccessDateTime.HasValue || api.LastErrorDateTime > api.LastSuccessDateTime);

            if (!hasError)
            {
                return true;
            }

            return api.LastSuccessDateTime.HasValue && api.LastSuccessDateTime > now.AddHours(-48);
        }

        public Task<List<X402Api>> GetDueForCheckAsync(DateTimeOffset now)
        {
            return dbContext.X402Apis
                .Where(x => x.NextCheckDateTime == null || x.NextCheckDateTime <= now)
                .ToListAsync();
        }

        /// <summary>
        /// Adds a new x402 API url. Returns the entity or an error message.
        /// Guards against abuse: SSRF (private/loopback hosts), per-IP rate limiting
        /// and a maximum number of endpoints per domain.
        /// </summary>
        public async Task<(X402Api? Api, string? Error)> AddX402ApiAsync(string url, string? clientIp = null)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (null, "Invalid or missing URL.");
            }

            // Strip query string and fragment so the same resource cannot be registered twice.
            var normalizedUrl = uri.GetLeftPart(UriPartial.Path);

            if (!string.IsNullOrEmpty(clientIp) && !IsRateLimitExempt(clientIp) && !TryAllowAdd(clientIp))
            {
                return (null, $"Too many URLs added. Try again later (max {MaxAddsPerHourPerIp} per hour).");
            }

            if (!await IsPublicHostAsync(uri.Host))
            {
                return (null, "The host is not a publicly reachable address.");
            }

            var existing = await dbContext.X402Apis
                .FirstOrDefaultAsync(x => x.Url.ToLower() == normalizedUrl.ToLower());

            if (existing != null)
            {
                return (null, "This URL is already registered.");
            }

            var domain = uri.Host;
            var domainCount = await dbContext.X402Apis.CountAsync(x => x.Domain == domain);
            if (domainCount >= MaxApisPerDomain)
            {
                return (null, $"This domain already has {MaxApisPerDomain} registered endpoints.");
            }

            var api = new X402Api
            {
                Url = normalizedUrl,
                Domain = domain,
                AddedDateTime = DateTimeOffset.UtcNow,
                NextCheckDateTime = DateTimeOffset.UtcNow // check as soon as possible
            };

            dbContext.X402Apis.Add(api);
            await dbContext.SaveChangesAsync();

            return (api, null);
        }

        /// <summary>
        /// True when the host resolves to a public address. Blocks loopback, private,
        /// link-local and reserved addresses to prevent SSRF against internal services.
        /// </summary>
        private static async Task<bool> IsPublicHostAsync(string host)
        {
            var normalized = host.TrimEnd('.').ToLowerInvariant();

            if (normalized == "localhost" || normalized.EndsWith(".localhost") ||
                normalized.EndsWith(".local") || normalized.EndsWith(".internal"))
            {
                return false;
            }

            IPAddress[] addresses;
            if (IPAddress.TryParse(normalized, out var literal))
            {
                addresses = [literal];
            }
            else
            {
                try
                {
                    addresses = await Dns.GetHostAddressesAsync(normalized);
                }
                catch (Exception)
                {
                    return false; // unresolvable host
                }
            }

            return addresses.Length > 0 && addresses.All(IsPublicIp);
        }

        private static bool IsPublicIp(IPAddress address) => address switch
        {
            { IsIPv4MappedToIPv6: true } => IsPublicIp(address.MapToIPv4()),
            _ when IPAddress.IsLoopback(address) => false,
            _ when address.IsIPv6LinkLocal || address.IsIPv6SiteLocal => false,
            { AddressFamily: System.Net.Sockets.AddressFamily.InterNetwork } ip =>
                !IsPrivateIPv4(ip.MapToIPv4()),
            { AddressFamily: System.Net.Sockets.AddressFamily.InterNetworkV6 } =>
                !address.IsIPv6Teredo && !IsUniqueLocalIpv6(address),
            _ => false
        };

        private static bool IsPrivateIPv4(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
        }

        private static bool IsUniqueLocalIpv6(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC; // fc00::/7 unique local
        }

        /// <summary>
        /// 188.91.x.x is exempt from the per-IP rate limit.
        /// </summary>
        private static bool IsRateLimitExempt(string clientIp)
        {
            if (!System.Net.IPAddress.TryParse(clientIp, out var ip))
            {
                return false;
            }

            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && ip.GetAddressBytes()[0] == 188
                && ip.GetAddressBytes()[1] == 91;
        }

        /// <summary>
        /// Fixed-window per-IP rate limit using IMemoryCache.
        /// </summary>
        private bool TryAllowAdd(string clientIp)
        {
            if (IsRateLimitExempt(clientIp))
            {
                return true;
            }

            var key = $"x402api:add:{clientIp.ToLowerInvariant()}";

            var count = memoryCache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return 0;
            });

            if (count >= MaxAddsPerHourPerIp)
            {
                return false;
            }

            memoryCache.Set(key, count + 1, TimeSpan.FromHours(1));
            return true;
        }

        /// <summary>
        /// Checks all APIs that are due and updates the check results.
        /// </summary>
        public async Task CheckDueApisAsync()
        {
            var now = DateTimeOffset.UtcNow;

            var dueApis = await GetDueForCheckAsync(now);
            if (dueApis.Count == 0)
            {
                return;
            }

            logger.LogInformation("Checking {Count} x402 API(s)", dueApis.Count);

            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            foreach (var api in dueApis)
            {
                await CheckApiAsync(api, httpClient);
                api.NextCheckDateTime = DateTimeOffset.UtcNow.AddMinutes(Random.Shared.Next(24 * 60, 48 * 60 + 1));
            }

            await dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Aggregate counts for the registry header. Computed over the visible,
        /// checked APIs so the numbers match what the list shows.
        /// </summary>
        public async Task<(int TotalApis, int TotalDomains, int TotalNetworks)> GetX402ApiStatsAsync(int max = 500)
        {
            var apis = await GetCheckedX402ApisAsync(max);

            var domains = apis.Select(x => x.Domain).Distinct().Count();

            var networks = apis
                .Select(x => x.PaymentsJson)
                .Where(json => !string.IsNullOrEmpty(json))
                .SelectMany(json =>
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<PaymentRequirementJson>>(json, JsonOptions) ?? [];
                    }
                    catch (JsonException)
                    {
                        return [];
                    }
                })
                .Select(p => p.Network)
                .Where(network => !string.IsNullOrEmpty(network))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return (apis.Count, domains, networks);
        }

        private async Task CheckApiAsync(X402Api api, HttpClient httpClient)
        {
            api.LastCheckDateTime = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var response = await httpClient.GetAsync(api.Url, HttpCompletionOption.ResponseHeadersRead);
                stopwatch.Stop();

                if (response.StatusCode != System.Net.HttpStatusCode.PaymentRequired)
                {
                    MarkError(api, $"Expected HTTP 402 Payment Required but received {(int)response.StatusCode}.");
                    return;
                }

                string? rawJson;
                var (paymentRequired, headerJson, parseError) = ParsePaymentRequired(response);

                if (paymentRequired != null)
                {
                    rawJson = headerJson;
                    api.Version = paymentRequired.X402Version.ToString();
                    api.Description = !string.IsNullOrWhiteSpace(paymentRequired.Resource.Description)
                        ? Truncate(paymentRequired.Resource.Description, 1024)
                        : Truncate(paymentRequired.Resource.ServiceName, 1024);
                    api.ServiceName = Truncate(paymentRequired.Resource.ServiceName, 64);
                    api.PaymentsJson = JsonSerializer.Serialize(
                        paymentRequired.Accepts.Select(a => new
                        {
                            network = a.Network,
                            amount = a.Amount,
                            asset = a.Asset
                        }), JsonOptions);
                }
                else
                {
                    // Fall back to x402 v1: the 402 response body is plain JSON (not a base64 header).
                    var (v1RawJson, v1Version, v1Requirements, v1Error) = await ParseV1PaymentRequiredAsync(response);

                    if (v1Requirements == null)
                    {
                        MarkError(api, v1Error ?? parseError ?? "Could not parse the PAYMENT-REQUIRED response.");
                        return;
                    }

                    rawJson = v1RawJson;
                    api.Version = v1Version;
                    api.Description = null;
                    api.ServiceName = null;
                    api.PaymentsJson = JsonSerializer.Serialize(v1Requirements, JsonOptions);
                }

                api.ErrorMessage = null;
                api.RawJsonResponse = rawJson;
                api.LatencyMs = (int)stopwatch.ElapsedMilliseconds;
                api.LastSuccessDateTime = DateTimeOffset.UtcNow;
                api.LastErrorDateTime = null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking x402 API {Url}", api.Url);
                MarkError(api, ex.Message);
            }
        }

        /// <summary>
        /// Parses an x402 v1 402 response (JSON body with an accepts list).
        /// Returns the requirements in the same {network, amount, asset} shape as v2.
        /// </summary>
        private static async Task<(string? RawJson, string? Version, List<object>? Requirements, string? Error)> ParseV1PaymentRequiredAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return (null, null, null, "No PAYMENT-REQUIRED header and an empty response body.");
            }

            V1PaymentRequired? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<V1PaymentRequired>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return (null, null, null, null); // not v1 either
            }

            if (parsed?.Accepts is not { Count: > 0 })
            {
                return (null, null, null, null);
            }

            var requirements = parsed.Accepts
                .Select(a => (object)new
                {
                    network = a.Network,
                    amount = a.MaxAmountRequired,
                    asset = a.Asset
                })
                .ToList();

            return (body, "1", requirements, null);
        }

        private record V1PaymentRequired
        {
            public int X402Version { get; set; }
            public string? Error { get; set; }
            public List<V1Accepts>? Accepts { get; set; }
        }

        private record V1Accepts
        {
            public string? Scheme { get; set; }
            public string? Network { get; set; }
            public string? MaxAmountRequired { get; set; }
            public string? Asset { get; set; }
            public string? Description { get; set; }
        }

        private static (PaymentRequiredResponse? Response, string? RawJson, string? Error) ParsePaymentRequired(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues(PaymentRequiredHeader, out var headerValues))
            {
                return (null, null, "No PAYMENT-REQUIRED header in the 402 response.");
            }

            var headerValue = headerValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return (null, null, "Empty PAYMENT-REQUIRED header in the 402 response.");
            }

            string json;
            try
            {
                json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(headerValue));
            }
            catch
            {
                return (null, null, "PAYMENT-REQUIRED header is not valid base64.");
            }

            PaymentRequiredResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<PaymentRequiredResponse>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                return (null, json, $"Invalid JSON in PAYMENT-REQUIRED header: {ex.Message}");
            }

            if (parsed == null || parsed.X402Version != 2)
            {
                return (null, json, "Unsupported x402 version.");
            }

            return (parsed, json, null);
        }

        private static void MarkError(X402Api api, string message)
        {
            api.ErrorMessage = Truncate(message, 2048);
            api.LastErrorDateTime = DateTimeOffset.UtcNow;
        }

        private static string? Truncate(string? value, int maxLength)
            => string.IsNullOrEmpty(value) ? value
               : value.Length <= maxLength ? value
               : value[..maxLength];

        private record PaymentRequirementJson
        {
            public string? Network { get; set; }
            public string? Amount { get; set; }
            public string? Asset { get; set; }
        }
    }
}
