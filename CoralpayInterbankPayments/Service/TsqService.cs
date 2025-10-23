using Newtonsoft.Json;
using static CoralpayInterbankPayments.Model.TsqDTOs;
using System.Text;
using CoralpayInterbankPayments.Interface;
using CoralpayInterbankPayments.Helper;

namespace CoralpayInterbankPayments.Service
{
    public class TsqService : ITsqService
    {
        private readonly PgpWrapperService _pgpService;
        private readonly ILogger<TsqService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TsqService(
            PgpWrapperService pgpService,
            ILogger<TsqService> logger,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _pgpService = pgpService;
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<TsqResponse?> QueryTransactionStatusAsync(string sessionId)
        {
            try
            {
                var requestObject = new { sessionId };
                var requestJson = JsonConvert.SerializeObject(requestObject);
                _logger.LogInformation("TSQ Request (plain): {Json}", requestJson);
                FileLogger.Log($"TSQ Request (plain): {requestJson}");

                var encryptedPayload = await _pgpService.EncryptAsync(requestJson);
                _logger.LogInformation("TSQ Request (encrypted): {Payload}", encryptedPayload);
                FileLogger.Log($"TSQ Request (encrypted): {encryptedPayload}");

                var tsqUrl = _configuration["CoralPay:TsqUrl"];
                var secretKey = _configuration["CoralPay:SecretKey"];

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, tsqUrl)
                {
                    Content = new StringContent(encryptedPayload, Encoding.UTF8, "text/plain")
                };
                httpRequest.Headers.Add("SecretKey", secretKey);

                var response = await _httpClient.SendAsync(httpRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errMsg = $"TSQ request failed. Status: {response.StatusCode}";
                    _logger.LogError(errMsg);
                    FileLogger.Log(errMsg);
                    return null;
                }

                var encryptedResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("TSQ Response (encrypted): {Payload}", encryptedResponse);
                FileLogger.Log($"TSQ Response (encrypted): {encryptedResponse}");
                var decryptedResponse = await _pgpService.DecryptAsync(encryptedResponse);
                _logger.LogInformation("TSQ Response (decrypted): {Json}", decryptedResponse);
                FileLogger.Log($"TSQ Response (decrypted): {decryptedResponse}");

                return JsonConvert.DeserializeObject<TsqResponse>(decryptedResponse);
            }
            catch (Exception ex)
            {
                var errMsg = $"Error calling CoralPay TSQ API: {ex.Message}";
                _logger.LogError(ex, errMsg);
                FileLogger.Log(errMsg);
                return null;
            }
        }
    }
}
