using CoralPayInterbankPayment.Model;
using static CoralpayInterbankPayments.Model.TsqDTOs;
using static SunTrustProxy;

namespace CoralPayInterbankPayment.Interface
{
    public interface ICipIncomingService
    {
        
        Task<string> ProcessNameEnquiryAsync(string encryptedPayload);
        //Task<NameEnquiryResponses> HandleNameEnquiryAsync(NameEnquiryRequest request);
        Task<string> HandleCreditAsync(string encryptedPayload);
        Task<string> HandleTransactionQueryAsync(string encryptedPayload);
        //Task<TsqResponse> QueryTransactionStatusAsync(string sessionId);
        /* Task<string> HandleNameEnquiryAsync(string encryptedPayload);
         Task<string> HandlePostCreditAsync(string encryptedPayload);
         Task<string> HandleTransactionQueryAsync(string encryptedPayload);*/
    }
}
