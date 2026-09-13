using Mapcars.Application.SavedPlaces.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class SavedPlaceRepository : GenericRepository<SavedPlace>, ISavedPlaceRepository
{
    public SavedPlaceRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SavedPlace>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderBy(p => p.Label)
            .ToListAsync(ct);
}
