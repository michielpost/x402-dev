using ProtoBuf;

namespace x402dev.Shared.Models
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PublicMessage
    {
        public string? Payer { get; set; }
        public string? Transaction { get; set; }
        public string? Network { get; set; }
        public string? Asset { get; set; }
        public string? Value { get; set; }

        public string? Name { get; set; }
        public required string Message { get; set; }
        public string? Link { get; set; }

        public required DateTime CreatedDateTime { get; set; }
    }
}
