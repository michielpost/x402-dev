using System.ComponentModel.DataAnnotations;

namespace x402dev.Database.Models
{
    public class X402Api
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(512)]
        public required string Url { get; set; }

        [MaxLength(255)]
        public required string Domain { get; set; }

        [MaxLength(1024)]
        public string? Description { get; set; }

        [MaxLength(64)]
        public string? ServiceName { get; set; }

        [MaxLength(16)]
        public string? Version { get; set; }

        /// <summary>
        /// JSON list of accepted payment requirements: [{ network, amount, asset }]
        /// </summary>
        public string? PaymentsJson { get; set; }

        public required DateTimeOffset AddedDateTime { get; set; }
        public DateTimeOffset? LastCheckDateTime { get; set; }
        public DateTimeOffset? NextCheckDateTime { get; set; }
        public DateTimeOffset? LastSuccessDateTime { get; set; }
        public DateTimeOffset? LastErrorDateTime { get; set; }

        public string? ErrorMessage { get; set; }

        /// <summary>
        /// HTTP method that produced a valid 402 response. Empty until detected,
        /// then the check always reuses this method (GET, POST, PUT or DELETE).
        /// </summary>
        [MaxLength(10)]
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Duration of the last successful check in milliseconds.
        /// </summary>
        public int? LatencyMs { get; set; }

        /// <summary>
        /// Raw JSON of the last x402 PAYMENT-REQUIRED response
        /// </summary>
        public string? RawJsonResponse { get; set; }
    }
}
