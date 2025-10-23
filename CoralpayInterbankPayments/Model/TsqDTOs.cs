namespace CoralpayInterbankPayments.Model
{
    public class TsqDTOs
    {
        public class TsqRequest
        {
            public string? SessionId { get; set; }
        }

        public class TsqResponse
        {
            public string? SessionId { get; set; }
            public string? PaymentRef { get; set; }
            public string? ResponseCode { get; set; }
            public string? ResponseMessage { get; set; }
        }

    }
}
