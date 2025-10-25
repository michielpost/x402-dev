using Swashbuckle.AspNetCore.Filters;
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

        [MaxLength(255)]
        [Url]
        public string? Link { get; set; }
    }

    public class PublicMessageRequestExample : IExamplesProvider<PublicMessageRequest>
    {
        public PublicMessageRequest GetExamples()
        {
            return new PublicMessageRequest
            {
                Name = "x402 builder",
                Message = "This is a sample public message!",
                Link = "https://x402dev.com"
            };
        }
    }
}
