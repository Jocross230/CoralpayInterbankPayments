
namespace CoralpayInterbankPayments.Service
{
   
    
        public static class PgpFormatHelper
        {
            public static bool LooksLikeArmored(string s)
                => !string.IsNullOrEmpty(s) && s.Contains("-----BEGIN PGP MESSAGE-----");

            public static bool LooksLikeHex(string s)
                => !string.IsNullOrEmpty(s) && s.Length % 2 == 0 && s.All(c => Uri.IsHexDigit(c));

            public static byte[] HexToBytes(string hex)
            {
                return Enumerable.Range(0, hex.Length / 2)
                    .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                    .ToArray();
            }

            public static string BytesToHex(byte[] data)
                => BitConverter.ToString(data).Replace("-", string.Empty);

            public static string JsonSafeEncode(string armored)
                => armored.Replace("\r\n", "\n").Replace("\n", "\\n");

            public static string JsonSafeDecode(string jsonSafe)
                => jsonSafe.Replace("\\n", "\n");
        }
    
}
