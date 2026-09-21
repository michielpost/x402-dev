using ProtoBuf;

namespace x402dev.Shared.Models
{
    /// <summary>
    /// Which server-side list the <see cref="X402Api"/> grid shows.
    /// </summary>
    public enum ListMode
    {
        All,
        Problems
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record X402ApiPaymentRequirement
    {
        public required string Network { get; set; }
        public required string Amount { get; set; }
        public required string Asset { get; set; }

        // Server-resolved display fields (empty when the raw data could not be parsed).
        public string? NetworkName { get; set; }
        public string? AmountFormatted { get; set; }
        public string? AssetName { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record X402Api
    {
        public int Id { get; set; }
        public required string Url { get; set; }
        public required string Domain { get; set; }
        public string? Description { get; set; }
        public string? ServiceName { get; set; }
        public string? Version { get; set; }
        public List<X402ApiPaymentRequirement> Requirements { get; set; } = new();

        public DateTime Added { get; set; }
        public DateTime? LastCheck { get; set; }
        public DateTime? NextCheck { get; set; }
        public DateTime? LastSuccess { get; set; }
        public DateTime? LastError { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RawJsonResponse { get; set; }
        public int? LatencyMs { get; set; }
        public string? HttpMethod { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record AddX402ApiRequest
    {
        public required string Url { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record AddX402ApiResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record GetX402ApiDetailRequest
    {
        public required string Url { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record GetX402ApiDetailResult
    {
        public bool Found { get; set; }
        public X402Api? Api { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record GetX402ApisByDomainRequest
    {
        public required string Domain { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record X402ApiStats
    {
        public int TotalApis { get; set; }
        public int TotalDomains { get; set; }
        public int TotalNetworks { get; set; }
        public int TotalErrors { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record GetX402ApisPagedRequest
    {
        public int Skip { get; set; }
        public int Take { get; set; }
        public string? Search { get; set; }
        public string? Domain { get; set; }
        public string? ExcludeUrl { get; set; }

        /// <summary>
        /// Property names to sort by, e.g. "Url" or "-LastCheck" (dash = descending).
        /// </summary>
        public List<string> SortBy { get; set; } = new();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record GetX402ApisPagedResult
    {
        public List<X402Api> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
