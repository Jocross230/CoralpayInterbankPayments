namespace CoralPayInterbankPayment.Model
{
    public class NameEnquiryRequest
    {
        public string? SessionId { get; set; }
        public string? DestinationInstitutionId { get; set; }
        public string? accountId { get; set; }
    }
}
