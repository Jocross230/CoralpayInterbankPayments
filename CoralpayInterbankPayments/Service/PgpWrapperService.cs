using CoralPay.Cryptography.Pgp;
using CoralPay.Cryptography.Pgp.Models;
using Lucene.Net.Support;

namespace CoralpayInterbankPayments.Service
{
    public class PgpWrapperService
    {
        private readonly string _publicKeyPath;
        private readonly string _privateKeyPath;
        private readonly string _internalKeyPassword;

        public PgpWrapperService(IConfiguration config)
        {
            _publicKeyPath = config["PGP:PublicKeyPath"]!;
            _privateKeyPath = config["PGP:PrivateKeyPath"]!;
            _internalKeyPassword = config["PGP:PrivateKeyPassphrase"]!;
        }

        public async Task<string> EncryptAsync(string plainText)
        {
            var enc = await Invoke.Encrypt(new EncryptionParam
            {
                ToEncryptText = plainText,
                ExternalPublicKeyPath = _publicKeyPath
            });
            return enc.Header.ResponseCode == "00" ? enc.Encryption : "96";
        }

        public async Task<string> DecryptAsync(string encrypted)
        {
            var dec = await Invoke.Decrypt(new DecryptionParam
            {
                EncryptedData = encrypted,
                InternalKeyPassword = _internalKeyPassword,
                InternalPrivateKey = _privateKeyPath,
                InternalPublicKey = _publicKeyPath
            });
            return dec.Header.ResponseCode == "00" ? dec.Decryption : "96";
        }
    }
}
