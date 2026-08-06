using Mapcars.Application.Ratings.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class RatingRepository : GenericRepository<Rating>, IRatingRepository
{
    public RatingRepository(AppDbContext context) : base(context) { }

    public async Task<Rating?> GetByTripAndRaterTypeAsync(Guid tripId, string raterType, CancellationToken ct = default)
        => await Set.FirstOrDefaultAsync(r => r.TripId == tripId && r.RaterType == raterType, ct);

    public async Task<IReadOnlyList<Rating>> ListForTripAsync(Guid tripId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(r => r.TripId == tripId)
            .ToListAsync(ct);
}
