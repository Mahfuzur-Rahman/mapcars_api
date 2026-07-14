using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class TripRepository : GenericRepository<Trip>, ITripRepository
{
    public TripRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Trip>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.RiderId == riderId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);
}
