using ProtoBuf;

namespace x402dev.Shared.Models
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public record Facilitator
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
        public bool NeedsApiKey { get; set; }
        public string? Comments { get; set; }

        public DateTime? Checked { get; set; }
        public DateTime? NextCheck { get; set; }

        public List<string> Kinds { get; set; } = new();
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public int ErrorCount { get; set; }
    }
}
