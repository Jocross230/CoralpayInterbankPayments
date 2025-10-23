namespace CoralpayInterbankPayments.Model
{
    public class EncryptionResponse
    {
        public ResponseHeader Header { get; set; } = new ResponseHeader();
        public string Encryption { get; set; } = string.Empty;
    }
}
