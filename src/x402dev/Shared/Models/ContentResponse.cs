using ProtoBuf;

namespace x402dev.Shared.Models
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]

    public class ContentResponse
    {
        public string? Text { get; set; }
    }
}
