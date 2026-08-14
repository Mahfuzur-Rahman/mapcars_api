using Mapcars.Application.Messages.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class MessageRepository : GenericRepository<TripMessage>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TripMessage>> ListForTripAsync(Guid tripId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(m => m.TripId == tripId)
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync(ct);
}
