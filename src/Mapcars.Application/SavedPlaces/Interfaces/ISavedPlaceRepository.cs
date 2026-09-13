using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.SavedPlaces.Interfaces;

public interface ISavedPlaceRepository : IGenericRepository<SavedPlace>
{
    Task<IReadOnlyList<SavedPlace>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
}
