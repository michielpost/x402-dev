using System.ComponentModel.DataAnnotations;

namespace x402dev.Database.Models
{
    public class PublicMessage
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(128)]
        public string? Payer { get; set; }
        [MaxLength(128)]
        public string? Transaction { get; set; }
        [MaxLength(128)]
        public string? Network { get; set; }
        [MaxLength(128)]
        public string? Asset { get; set; }
        [MaxLength(64)]
        public string? Value { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }
        [MaxLength(255)]
        public required string Message { get; set; }
        [MaxLength(255)]
        public string? Link { get; set; }

        public required DateTimeOffset CreatedDateTime { get; set; }

    }
}