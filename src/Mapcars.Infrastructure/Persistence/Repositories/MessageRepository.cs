using Mapcars.Application.Messages.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <remarks>
/// The unread predicate here is <c>ChatPolicy.IsFromCounterpart</c> + "sent after my
/// read time", written as SQL-translatable LINQ. Change one, change both.
/// Sender types: drivers are always "driver"; the customer is "customer" or legacy "rider".
/// </remarks>
public class MessageRepository : GenericRepository<TripMessage>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TripMessage>> ListForTripAsync(Guid tripId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(m => m.TripId == tripId)
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ConversationRow>> ListConversationsAsync(
        string viewerType, Guid viewerId, Guid? tripId, int limit, CancellationToken ct = default)
    {
        var asDriver = UserTypes.IsDriver(viewerType);
        var driver = UserTypes.Driver;

        var trips = Context.Trips.AsNoTracking()
            .Where(t => asDriver ? t.DriverId == viewerId : t.CustomerId == viewerId);
        trips = tripId is { } id
            ? trips.Where(t => t.Id == id)
            : trips.Where(t => Set.Any(m => m.TripId == t.Id));

        return await trips
            .OrderByDescending(t => Set.Where(m => m.TripId == t.Id).Max(m => (DateTime?)m.SentAtUtc))
            .Take(limit)
            .Select(t => new ConversationRow(
                t.Id,
                t.Status,
                t.CreatedAtUtc,
                t.CompletedAtUtc,
                t.PickupAddress,
                t.DropoffAddress,
                t.Customer != null ? t.Customer.FullName : null,
                t.Driver != null ? t.Driver.FullName : null,
                Set.Where(m => m.TripId == t.Id).OrderByDescending(m => m.SentAtUtc).Select(m => m.Content).FirstOrDefault(),
                Set.Where(m => m.TripId == t.Id).OrderByDescending(m => m.SentAtUtc).Select(m => m.SenderType).FirstOrDefault(),
                Set.Where(m => m.TripId == t.Id).Max(m => (DateTime?)m.SentAtUtc),
                Set.Count(m => m.TripId == t.Id
                    && (asDriver ? m.SenderType != driver : m.SenderType == driver)
                    && (asDriver
                        ? t.DriverChatReadAtUtc == null || m.SentAtUtc > t.DriverChatReadAtUtc
                        : t.CustomerChatReadAtUtc == null || m.SentAtUtc > t.CustomerChatReadAtUtc))))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UnreadTripCount>> CountUnreadByTripAsync(
        string viewerType, Guid viewerId, CancellationToken ct = default)
    {
        var asDriver = UserTypes.IsDriver(viewerType);
        var driver = UserTypes.Driver;

        return await Set.AsNoTracking()
            .Where(m => asDriver ? m.Trip!.DriverId == viewerId : m.Trip!.CustomerId == viewerId)
            .Where(m => asDriver ? m.SenderType != driver : m.SenderType == driver)
            .Where(m => asDriver
                ? m.Trip!.DriverChatReadAtUtc == null || m.SentAtUtc > m.Trip.DriverChatReadAtUtc
                : m.Trip!.CustomerChatReadAtUtc == null || m.SentAtUtc > m.Trip.CustomerChatReadAtUtc)
            .GroupBy(m => m.TripId)
            .Select(g => new UnreadTripCount(g.Key, g.Count()))
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(Guid tripId, string viewerType, DateTime atUtc, CancellationToken ct = default)
    {
        DateTime? at = atUtc;
        var trip = Context.Trips.Where(t => t.Id == tripId);
        if (UserTypes.IsDriver(viewerType))
            await trip.ExecuteUpdateAsync(s => s.SetProperty(t => t.DriverChatReadAtUtc, at), ct);
        else
            await trip.ExecuteUpdateAsync(s => s.SetProperty(t => t.CustomerChatReadAtUtc, at), ct);
    }
}
