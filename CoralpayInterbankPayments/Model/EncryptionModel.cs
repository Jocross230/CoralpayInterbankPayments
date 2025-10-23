namespace CoralPayInterbankPayment.Model
{
    public class EncryptionModel
    {
        public class EncryptRequest
        {
            public string? PlainText { get; set; }
        }

        public class DecryptRequest
        {
            public string? EncryptedText { get; set; }
        }
    }
}
