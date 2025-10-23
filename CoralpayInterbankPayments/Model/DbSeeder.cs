/*using CoralPayInterbankPayment.Data;
using CoralPayInterbankPayment.Model;

namespace CoralpayInterbankPayments.Model
{
    public static class DbSeeder
    {
        public static void Seed(CreditDbContext db)
        {
            // If already has data, don’t insert again
            if (db.FTSingleRequests.Any()) return;

            var testTxn = new FTSingleRequest
            {
                Id = Guid.NewGuid(),
                sessionId = "TEST123456",
                paymentRef = "PAYREF001",
                destinationInstitutionId = "999001",
                creditAccount = "1234567890",
                creditAccountName = "Test User",
                sourceAccountId = "0987654321",
                sourceAccountName = "Source User",
                narration = "Test transaction",
                channel = "WEB",
                group = "TestGroup",
                sector = "IT",
                amount = 100.50m,
                nameEnquiryRef = "ENQ001",
                transactionDate = DateTime.UtcNow,
                responseCode = "PENDING",
                responseMessage = "Pending"
            };

            db.FTSingleRequests.Add(testTxn);
            db.SaveChanges();
        }
    }
}
*/