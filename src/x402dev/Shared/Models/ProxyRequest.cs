using System.ComponentModel.DataAnnotations;

namespace x402dev.Server.Models
{
    public class ProxyRequest
    {
        [Url]
        public string Url { get; set; } = string.Empty;
        public string? PaymentHeader { get; set; }

    }

    public class ProxyResponse
    {
        public Dictionary<string, string> Headers { get; set; } = new();
        public string Content { get; set; } = string.Empty;
        public int StatusCode { get; set; }
    }
}
