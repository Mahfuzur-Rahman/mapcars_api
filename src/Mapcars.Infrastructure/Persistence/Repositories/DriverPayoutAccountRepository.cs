using Mapcars.Application.Payments.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class DriverPayoutAccountRepository : GenericRepository<DriverPayoutAccount>, IDriverPayoutAccountRepository
{
    public DriverPayoutAccountRepository(AppDbContext context) : base(context) { }

    public Task<DriverPayoutAccount?> FindByDriverIdAsync(Guid driverId, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(a => a.DriverId == driverId, ct);

    public Task<DriverPayoutAccount?> FindByStripeAccountIdAsync(string stripeAccountId, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(a => a.StripeAccountId == stripeAccountId, ct);
}
