using CoralPayInterbankPayment.Model;
using Microsoft.EntityFrameworkCore;
namespace CoralPayInterbankPayment.Data
{
    public class CreditDbContext : DbContext
    {
        public CreditDbContext (DbContextOptions<CreditDbContext> options) : base(options)
        {
        }
        public DbSet<FTSingleRequest> FTSingleRequests { get; set; } = default!;

    }

}
