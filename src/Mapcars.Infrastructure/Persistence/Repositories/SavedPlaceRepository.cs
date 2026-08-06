using Mapcars.Application.SavedPlaces.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class SavedPlaceRepository : GenericRepository<SavedPlace>, ISavedPlaceRepository
{
    public SavedPlaceRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SavedPlace>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(p => p.RiderId == riderId)
            .OrderBy(p => p.Label)
            .ToListAsync(ct);
}
