using System.ComponentModel.DataAnnotations;

namespace x402dev.Server.Controllers.SampleEndpoints.Models
{
    public class SampleRequest
    {
        [Required]
        public required string Value { get; set; }
    }
}
