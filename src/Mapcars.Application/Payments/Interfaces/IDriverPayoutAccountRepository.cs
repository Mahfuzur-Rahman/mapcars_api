using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Payments.Interfaces;

public interface IDriverPayoutAccountRepository : IGenericRepository<DriverPayoutAccount>
{
    Task<DriverPayoutAccount?> FindByDriverIdAsync(Guid driverId, CancellationToken ct = default);
    Task<DriverPayoutAccount?> FindByStripeAccountIdAsync(string stripeAccountId, CancellationToken ct = default);
}
