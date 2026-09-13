using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Messages.Interfaces;
using Mapcars.Application.Messages.Mapping;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Messages.Services;

/// <summary>
/// Business logic for in-trip chat messages. Either the customer or the driver on
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
    private readonly IPushService _push;

    public MessageService(
        IMessageRepository messages, ITripRepository trips, IUnitOfWork uow, ITripNotifier notifier,
        IPushService push)
    {
        _messages = messages;
        _trips = trips;
        _uow = uow;
        _notifier = notifier;
        _push = push;
    }

    public async Task<MessageResponse> SendAsync(
        string callerType, Guid callerId, Guid tripId, SendMessageRequest request, CancellationToken ct = default)
    {
        var trip = await GetParticipantTripAsync(callerType, callerId, tripId, ct);

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

        // SignalR only reaches a client with the app open and the socket up. A
        // customer whose phone is in their pocket, or a driver between jobs, sees
        // nothing at all — so the message also goes out as a push, to the other
        // party only. Best-effort, like the realtime one: chat must not fail
        // because a notification could not be delivered.
        NotifyCounterpartAsync(trip, callerType, request.Content, ct);

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

    /// <summary>
    /// Pushes a new message to whichever party did not send it.
    /// </summary>
    /// <remarks>
    /// Fire-and-forget by design, matching the realtime push above: the message
    /// is already persisted and returned, and a dead FCM token must not turn a
    /// successful send into a failed request.
    ///
    /// The body carries the message text so the notification is useful from the
    /// lock screen — the alternative ("You have a new message") makes the reader
    /// open the app to learn whether it mattered. Trip chat is short-lived and
    /// operational ("I'm at the side entrance"), not private correspondence.
    ///
    /// Sends nothing if the trip has no driver yet: an unassigned trip has no
    /// counterpart to notify.
    /// </remarks>
    private void NotifyCounterpartAsync(Trip trip, string senderType, string content, CancellationToken ct)
    {
        var isFromCustomer = senderType == UserTypes.Customer;
        var recipientType = isFromCustomer ? UserTypes.Driver : UserTypes.Customer;
        var recipientId = isFromCustomer ? trip.DriverId : trip.CustomerId;
        if (recipientId is null || recipientId == Guid.Empty) return;

        _ = _push.NotifyUserAsync(
            recipientType,
            recipientId.Value,
            new PushMessage(
                isFromCustomer ? "Message from your passenger" : "Message from your driver",
                content,
                new Dictionary<string, string>
                {
                    ["type"] = "messageReceived",
                    ["tripId"] = trip.Id.ToString(),
                }),
            ct);
    }

    private async Task<Trip> GetParticipantTripAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isParticipant = (callerType == UserTypes.Customer && trip.CustomerId == callerId)
            || (callerType == "driver" && trip.DriverId == callerId);
        if (!isParticipant)
            throw new NotFoundException("Trip", tripId);

        return trip;
    }
}
