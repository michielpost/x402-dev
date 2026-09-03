using Microsoft.AspNetCore.Http;
using Nethereum.Signer;
using ProtoBuf.Grpc;
using System.Text.Json;
using x402dev.Services;
using x402dev.Shared.Interfaces;
using X402ApiModel = x402dev.Shared.Models.X402Api;

namespace x402dev.Server.Services
{
    public class X402ApiGrpcService(X402ApiService x402ApiService,
        IHttpContextAccessor httpContextAccessor) : IX402ApiGrpcService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<List<X402ApiModel>> GetX402Apis(CallContext context = default)
        {
            var apis = await x402ApiService.GetCheckedX402ApisAsync();

            return apis.Select(Map).ToList();
        }

        public async Task<Shared.Models.AddX402ApiResult> AddX402Api(Shared.Models.AddX402ApiRequest request, CallContext context = default)
        {
            var (api, error) = await x402ApiService.AddX402ApiAsync(request.Url, GetClientIp());

            return new Shared.Models.AddX402ApiResult
            {
                Success = api != null,
                Error = error
            };
        }

        public async Task<Shared.Models.GetX402ApiDetailResult> GetX402ApiDetail(Shared.Models.GetX402ApiDetailRequest request, CallContext context = default)
        {
            var api = await x402ApiService.GetX402ApiByUrlAsync(request.Url);

            return new Shared.Models.GetX402ApiDetailResult
            {
                Found = api != null,
                Api = api == null ? null : Map(api)
            };
        }

        public async Task<List<X402ApiModel>> GetX402ApisByDomain(Shared.Models.GetX402ApisByDomainRequest request, CallContext context = default)
        {
            var apis = await x402ApiService.GetX402ApisByDomainAsync(request.Domain);

            return apis.Select(Map).ToList();
        }

        public async Task<List<X402ApiModel>> GetX402ApisWithProblems(CallContext context = default)
        {
            var apis = await x402ApiService.GetProblemX402ApisAsync();

            return apis.Select(Map).ToList();
        }

        public async Task<Shared.Models.X402ApiStats> GetX402ApiStats(CallContext context = default)
        {
            var (totalApis, totalDomains, totalNetworks) = await x402ApiService.GetX402ApiStatsAsync();

            return new Shared.Models.X402ApiStats
            {
                TotalApis = totalApis,
                TotalDomains = totalDomains,
                TotalNetworks = totalNetworks
            };
        }

        private string? GetClientIp()
        {
            return httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        }

        private static X402ApiModel Map(Database.Models.X402Api api)
        {
            var requirements = new List<Shared.Models.X402ApiPaymentRequirement>();

            if (!string.IsNullOrEmpty(api.PaymentsJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<PaymentRequirementJson>>(api.PaymentsJson, JsonOptions);
                    if (parsed != null)
                    {
                        requirements = parsed
                            .Select(p =>
                            {
                                var requirement = new Shared.Models.X402ApiPaymentRequirement
                                {
                                    Network = p.Network ?? string.Empty,
                                    Amount = p.Amount ?? string.Empty,
                                    Asset = p.Asset ?? string.Empty
                                };
                                ResolveDisplayFields(requirement);
                                return requirement;
                            })
                            .ToList();
                    }
                }
                catch (JsonException)
                {
                    // ignore malformed json
                }
            }

            return new X402ApiModel
            {
                Id = api.Id,
                Url = api.Url,
                Domain = api.Domain,
                Description = api.Description,
                ServiceName = api.ServiceName,
                Version = api.Version,
                Requirements = requirements,
                Added = api.AddedDateTime.DateTime,
                LastCheck = api.LastCheckDateTime?.DateTime,
                NextCheck = api.NextCheckDateTime?.DateTime,
                LastSuccess = api.LastSuccessDateTime?.DateTime,
                LastError = api.LastErrorDateTime?.DateTime,
                HasError = api.LastErrorDateTime.HasValue
                    && (!api.LastSuccessDateTime.HasValue || api.LastErrorDateTime > api.LastSuccessDateTime),
                ErrorMessage = api.ErrorMessage,
                RawJsonResponse = api.RawJsonResponse,
                LatencyMs = api.LatencyMs
            };
        }

        private static readonly x402.Core.AssetInfoProvider AssetInfoProvider = new();
        private static readonly Type ChainEnumType = typeof(Chain);

        /// <summary>
        /// Resolves human-readable network/asset names and formats the amount using the
        /// asset's decimals. Raw values are kept on the requirement as fallback.
        /// </summary>
        private static void ResolveDisplayFields(Shared.Models.X402ApiPaymentRequirement requirement)
        {
            // Asset name + amount formatting from the contract address.
            if (!string.IsNullOrWhiteSpace(requirement.Asset))
            {
                var assetInfo = AssetInfoProvider.GetAssetInfo(requirement.Asset);
                if (assetInfo != null && assetInfo.Decimals >= 0)
                {
                    requirement.AssetName = assetInfo.Name;

                    if (decimal.TryParse(requirement.Amount, System.Globalization.CultureInfo.InvariantCulture, out var amount))
                    {
                        requirement.AmountFormatted = (amount / (decimal)Math.Pow(10, assetInfo.Decimals))
                            .ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }

            // Chain name from an eip155 network identifier.
            if (requirement.Network.StartsWith("eip155:", StringComparison.OrdinalIgnoreCase))
            {
                var chainIdPart = requirement.Network["eip155:".Length..];
                if (int.TryParse(chainIdPart, out var chainId) && Enum.IsDefined(ChainEnumType, chainId))
                {
                    requirement.NetworkName = ((Chain)chainId).ToString();
                }
            }
        }

        private record PaymentRequirementJson
        {
            public string? Network { get; set; }
            public string? Amount { get; set; }
            public string? Asset { get; set; }
        }
    }
}
