using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Messages.Interfaces;

public interface IMessageRepository : IGenericRepository<TripMessage>
{
    Task<IReadOnlyList<TripMessage>> ListForTripAsync(Guid tripId, CancellationToken ct = default);
}
