using static CoralpayInterbankPayments.Model.TsqDTOs;

namespace CoralpayInterbankPayments.Interface
{
    public interface ITsqService
    {
        Task<TsqResponse?> QueryTransactionStatusAsync(string sessionId);
    }
}
