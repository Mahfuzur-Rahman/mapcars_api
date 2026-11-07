using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Messages.Interfaces;
using Mapcars.Application.Messages.Mapping;

namespace Mapcars.Application.Messages.Services;

/// <summary>
/// The inbox. Participation is enforced by the repository's viewer filter: a trip
/// the caller is not party to simply yields no row, which becomes a 404 — the same
/// "don't confirm it exists" answer <c>MessageService</c> gives.
/// </summary>
public class ConversationService : IConversationService
{
    /// <summary>The list is not paginated; this bounds it.</summary>
    private const int ListLimit = 100;

    private readonly IMessageRepository _messages;
    private readonly ChatOptions _chat;

    public ConversationService(IMessageRepository messages, ChatOptions chat)
    {
        _messages = messages;
        _chat = chat;
    }

    public async Task<IReadOnlyList<ConversationResponse>> ListAsync(
        string callerType, Guid callerId, CancellationToken ct = default)
    {
        var rows = await _messages.ListConversationsAsync(callerType, callerId, tripId: null, ListLimit, ct);
        var now = DateTime.UtcNow;
        return rows.Select(r => r.ToResponse(callerType, now, _chat.SendWindow)).ToList();
    }

    public async Task<ConversationResponse> GetAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
        => (await FindAsync(callerType, callerId, tripId, ct)).ToResponse(callerType, DateTime.UtcNow, _chat.SendWindow);

    public async Task<UnreadSummaryResponse> UnreadAsync(
        string callerType, Guid callerId, CancellationToken ct = default)
    {
        var counts = await _messages.CountUnreadByTripAsync(callerType, callerId, ct);
        return new UnreadSummaryResponse(
            counts.Sum(c => c.Count),
            counts.Select(c => new UnreadTripCountResponse(c.TripId, c.Count)).ToList());
    }

    public async Task MarkReadAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        await FindAsync(callerType, callerId, tripId, ct); // 404 for non-parties
        await _messages.MarkReadAsync(tripId, callerType, DateTime.UtcNow, ct);
    }

    private async Task<ConversationRow> FindAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct)
    {
        var rows = await _messages.ListConversationsAsync(callerType, callerId, tripId, 1, ct);
        return rows.Count > 0 ? rows[0] : throw new NotFoundException("Trip", tripId);
    }
}
