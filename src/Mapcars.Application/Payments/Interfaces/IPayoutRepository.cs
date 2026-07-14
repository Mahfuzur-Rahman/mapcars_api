using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Payments.Interfaces;

public interface IPayoutRepository : IGenericRepository<Payout>
{
    Task<IReadOnlyList<Payout>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<Payout?> FindByStripePayoutIdAsync(string stripePayoutId, CancellationToken ct = default);
}
