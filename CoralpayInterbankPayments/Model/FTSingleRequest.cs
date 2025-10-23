using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoralPayInterbankPayment.Model
{
    public class FTSingleRequest
    {
        [Key]
        [JsonIgnore]
        public Guid Id { get; set; }

        public string? sessionId { get; set; }
        public string? paymentRef { get; set; }
        public string? destinationInstitutionId { get; set; }
        public string? creditAccount { get; set; }
        public string? creditAccountName { get; set; }
        public string? sourceAccountId { get; set; }
        public string? sourceAccountName { get; set; }
        public string? narration { get; set; }
        public string? channel { get; set; }

        [Column("group")]
        public string? Group { get; set; }

        public string? sector { get; set; }
        public decimal? amount { get; set; }
        public string? nameEnquiryRef { get; set; }

        [JsonIgnore]
        public DateTime? transactionDate { get; set; } = DateTime.UtcNow;

        [NotMapped]
        [JsonProperty("transactionDate")]
        public string? TransactionDateFormatted
            => transactionDate?.ToString("yyyy-MM-dd HH:mm:ss.fff");

        public string? responseCode { get; set; }
        public string? responseMessage { get; set; }
    }
}
