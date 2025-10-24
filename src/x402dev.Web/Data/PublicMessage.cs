using System.ComponentModel.DataAnnotations;

namespace x402dev.Web.Data
{
    public class PublicMessage
    {
        [Key]
        public int Id { get; set; }

        public string? Payer { get; set; }
        public string? Transaction { get; set; }
        public string? Network { get; set; }
        public string? Asset { get; set; }
        public string? Value { get; set; }

        public string? Name { get; set; }
        public required string Message { get; set; }

        public required DateTimeOffset CreatedDateTime { get; set; }

    }
}