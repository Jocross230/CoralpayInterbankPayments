namespace CoralPayInterbankPayment.Model
{
    public class CreditRequest
    {
        public string? sessionId { get; set; }
        public string? paymentRef { get; set; }
        public string? destinationInstitutionId { get; set; }
        public string? creditAccount { get; set; }
        public string? creditAccountName { get; set; }
        public string? sourceAccountId { get; set; }
        public string? sourceAccountName { get; set; }
        public string? narration { get; set; }
        public string? channel { get; set; }
        public string? group { get; set; }
        public string? sector { get; set; }
        public decimal amount { get; set; }
        public string? nameEnquiryRef { get; set; }
    }
}

