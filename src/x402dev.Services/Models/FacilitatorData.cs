using x402.Core.Models.Facilitator;

namespace x402dev.Services.Models
{
    public record FacilitatorData
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
        public bool NeedsApiKey { get; set; }
        public string? Comments { get; set; }



        public DateTimeOffset? Checked { get; set; }
        public DateTimeOffset? NextCheck { get; set; }

        public List<FacilitatorKind> Kinds { get; set; } = new();
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public int ErrorCount { get; set; }
    }
}
