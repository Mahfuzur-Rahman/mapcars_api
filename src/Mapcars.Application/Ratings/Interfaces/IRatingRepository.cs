using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Ratings.Interfaces;

public interface IRatingRepository : IGenericRepository<Rating>
{
    Task<Rating?> GetByTripAndRaterTypeAsync(Guid tripId, string raterType, CancellationToken ct = default);
    Task<IReadOnlyList<Rating>> ListForTripAsync(Guid tripId, CancellationToken ct = default);
}
