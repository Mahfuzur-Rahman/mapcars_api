using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Messages.Interfaces;
using Mapcars.Application.Messages.Mapping;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Messages.Services;

/// <summary>
/// Business logic for in-trip chat messages. Either the rider or the driver on
/// an active trip may send and list messages. No trip-status restriction beyond
/// "the caller is a participant" — chat is available from assignment through
/// completion.
/// </summary>
public class MessageService : IMessageService
{
    private readonly IMessageRepository _messages;
    private readonly ITripRepository _trips;
    private readonly IUnitOfWork _uow;
    private readonly ITripNotifier _notifier;

    public MessageService(
        IMessageRepository messages, ITripRepository trips, IUnitOfWork uow, ITripNotifier notifier)
    {
        _messages = messages;
        _trips = trips;
        _uow = uow;
        _notifier = notifier;
    }

    public async Task<MessageResponse> SendAsync(
        string callerType, Guid callerId, Guid tripId, SendMessageRequest request, CancellationToken ct = default)
    {
        await GetParticipantTripAsync(callerType, callerId, tripId, ct);

        var message = new TripMessage
        {
            TripId = tripId,
            SenderType = callerType,
            SenderId = callerId,
            Content = request.Content,
            SentAtUtc = DateTime.UtcNow,
        };
        await _messages.AddAsync(message, ct);
        await _uow.SaveChangesAsync(ct);

        var response = message.ToResponse();

        // Best-effort realtime push — fire and forget so a SignalR hiccup never
        // fails the send. The message is already persisted.
        _ = _notifier.MessageReceivedAsync(tripId, response, ct);

        return response;
    }

    public async Task<IReadOnlyList<MessageResponse>> ListForTripAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        await GetParticipantTripAsync(callerType, callerId, tripId, ct);
        var messages = await _messages.ListForTripAsync(tripId, ct);
        return messages.Select(m => m.ToResponse()).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Trip> GetParticipantTripAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isParticipant = (callerType == "rider" && trip.RiderId == callerId)
            || (callerType == "driver" && trip.DriverId == callerId);
        if (!isParticipant)
            throw new NotFoundException("Trip", tripId);

        return trip;
    }
}
