using Mapcars.Application.Payments.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class PayoutRepository : GenericRepository<Payout>, IPayoutRepository
{
    public PayoutRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Payout>> ListForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(p => p.DriverId == driverId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<Payout?> FindByStripePayoutIdAsync(string stripePayoutId, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(p => p.StripePayoutId == stripePayoutId, ct);
}
