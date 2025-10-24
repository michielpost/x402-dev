using System.ComponentModel.DataAnnotations;

namespace x402dev.Web.Models
{
    public class PublicMessageRequest
    {
        [MaxLength(32)]
        public string? Name { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Message { get; set; }
    }
}
