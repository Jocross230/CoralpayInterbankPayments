namespace CoralpayInterbankPayments.Model
{
    public class DecryptionResponse
    {
        public ResponseHeader Header { get; set; } = new ResponseHeader();
        public string Decryption { get; set; } = string.Empty;
    }
}
