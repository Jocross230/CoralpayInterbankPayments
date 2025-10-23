namespace CoralpayInterbankPayments.Model
{
    public class DecryptionParam
    {
        public string EncryptedData { get; set; } = string.Empty;
        public string InternalKeyPassword { get; set; } = string.Empty;
        public string InternalPrivateKey { get; set; } = string.Empty;
        public string? InternalPublicKey { get; set; } = null;
        public InputFormat InputFormat { get; set; } = InputFormat.AutoDetect;
    }

    public enum InputFormat
    {
        AutoDetect,
        Armored,
        Base64,
        Hex,
        JsonSafe
    }
}
