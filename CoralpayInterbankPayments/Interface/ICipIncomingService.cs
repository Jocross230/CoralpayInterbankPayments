using CoralPayInterbankPayment.Model;
using static CoralpayInterbankPayments.Model.TsqDTOs;
using static SunTrustProxy;

namespace CoralPayInterbankPayment.Interface
{
    public interface ICipIncomingService
    {
        
        Task<string> ProcessNameEnquiryAsync(string encryptedPayload);
        Task<string> HandleCreditAsync(string encryptedPayload);
        Task<string> HandleTransactionQueryAsync(string encryptedPayload);
        
    }
}
