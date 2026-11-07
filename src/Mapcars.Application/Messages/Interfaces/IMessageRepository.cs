using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Messages.Interfaces;

/// <summary>A conversation as read from the database, before per-viewer shaping.</summary>
public sealed record ConversationRow(
    Guid TripId,
    TripStatus Status,
    DateTime TripCreatedAtUtc,
    DateTime? CompletedAtUtc,
    string PickupAddress,
    string DropoffAddress,
    string? CustomerName,
    string? DriverName,
    string? LastContent,
    string? LastSenderType,
    DateTime? LastSentAtUtc,
    int UnreadCount);

public sealed record UnreadTripCount(Guid TripId, int Count);

public interface IMessageRepository : IGenericRepository<TripMessage>
{
    Task<IReadOnlyList<TripMessage>> ListForTripAsync(Guid tripId, CancellationToken ct = default);

    /// <summary>
    /// The viewer's conversations, newest message first. With <paramref name="tripId"/>
    /// null: only trips that have at least one message. With a trip id: that trip
    /// even if it has no messages yet — and nothing if the viewer is not its party
    /// (the caller turns that into a 404).
    /// </summary>
    Task<IReadOnlyList<ConversationRow>> ListConversationsAsync(
        string viewerType, Guid viewerId, Guid? tripId, int limit, CancellationToken ct = default);

    /// <summary>Unread counts per trip for the viewer; trips with none are omitted.</summary>
    Task<IReadOnlyList<UnreadTripCount>> CountUnreadByTripAsync(
        string viewerType, Guid viewerId, CancellationToken ct = default);

    /// <summary>Stamps the viewer's read time on the trip. Does not check participation.</summary>
    Task MarkReadAsync(Guid tripId, string viewerType, DateTime atUtc, CancellationToken ct = default);
}
