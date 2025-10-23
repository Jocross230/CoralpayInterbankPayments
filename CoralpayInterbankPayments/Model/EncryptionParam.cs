namespace CoralpayInterbankPayments.Model
{
    public class EncryptionParam
    {
        public string ToEncryptText { get; set; } = string.Empty;
        public string ExternalPublicKeyPath { get; set; } = string.Empty;
        public PgpOutputFormat OutputFormat { get; set; } = PgpOutputFormat.Hex;
    }

    public enum PgpOutputFormat
    {
        Armored,
        Base64,
        Hex,
        JsonSafe
    }
}
