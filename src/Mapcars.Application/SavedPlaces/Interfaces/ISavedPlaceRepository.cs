using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.SavedPlaces.Interfaces;

public interface ISavedPlaceRepository : IGenericRepository<SavedPlace>
{
    Task<IReadOnlyList<SavedPlace>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);
}
