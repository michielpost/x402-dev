using System.ComponentModel.DataAnnotations;

namespace x402dev.Client.Models
{
    public class PublicMessageRequest
    {
        [MaxLength(32)]
        public string? Name { get; set; }

        [Required]
        [MaxLength(255)]
        public string? Message { get; set; }

        [MaxLength(255)]
        [Url]
        public string? Link { get; set; }
    }
   
}
