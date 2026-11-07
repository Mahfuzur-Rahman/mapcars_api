using System.Globalization;
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
/// Business logic for trip chat messages. Either the customer or the driver on a
/// trip may list its messages at any time. Sending follows <see cref="ChatPolicy.CanSend"/>:
/// open from assignment until <see cref="ChatOptions.SendWindow"/> after completion
/// (lost property), closed for cancelled/expired trips.
/// </summary>
public class MessageService : IMessageService
{
    private readonly IMessageRepository _messages;
    private readonly ITripRepository _trips;
    private readonly IUnitOfWork _uow;
    private readonly ITripNotifier _notifier;
    private readonly IPushService _push;
    private readonly ChatOptions _chat;

    public MessageService(
        IMessageRepository messages, ITripRepository trips, IUnitOfWork uow, ITripNotifier notifier,
        IPushService push, ChatOptions chat)
    {
        _messages = messages;
        _trips = trips;
        _uow = uow;
        _notifier = notifier;
        _push = push;
        _chat = chat;
    }

    public async Task<MessageResponse> SendAsync(
        string callerType, Guid callerId, Guid tripId, SendMessageRequest request, CancellationToken ct = default)
    {
        var trip = await GetParticipantTripAsync(callerType, callerId, tripId, ct);

        var now = DateTime.UtcNow;
        if (!ChatPolicy.CanSend(trip.Status, trip.CompletedAtUtc, now, _chat.SendWindow))
            throw new ConflictException("This chat has closed.");

        var message = new TripMessage
        {
            TripId = tripId,
            SenderType = callerType,
            SenderId = callerId,
            Content = request.Content,
            SentAtUtc = now,
        };
        await _messages.AddAsync(message, ct);
        await _uow.SaveChangesAsync(ct);

        // Writing is reading: the sender has seen everything up to their own message.
        await _messages.MarkReadAsync(tripId, callerType, now, ct);

        var response = message.ToResponse();

        // Best-effort realtime push — fire and forget so a SignalR hiccup never
        // fails the send. The message is already persisted.
        _ = _notifier.MessageReceivedAsync(tripId, response, ct);

        // SignalR only reaches a client with the trip open and the socket up. The
        // push reaches everyone else — and, in the foreground, the apps read the
        // whole message out of its data to update the inbox without a round trip.
        await NotifyCounterpartAsync(trip, response, ct);

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
    /// The push itself is fire-and-forget, matching the realtime one: the message
    /// is already persisted and returned, and a dead FCM token must not turn a
    /// successful send into a failed request.
    ///
    /// The body carries the message text so the notification is useful from the
    /// lock screen — the alternative ("You have a new message") makes the reader
    /// open the app to learn whether it mattered. Trip chat is short-lived and
    /// operational ("I'm at the side entrance"), not private correspondence.
    ///
    /// The data carries the whole message plus the recipient's unread total, so a
    /// foregrounded app can update its inbox and badge without a round trip.
    ///
    /// Sends nothing if the trip has no driver yet: an unassigned trip has no
    /// counterpart to notify.
    /// </remarks>
    private async Task NotifyCounterpartAsync(Trip trip, MessageResponse message, CancellationToken ct)
    {
        var isFromCustomer = !UserTypes.IsDriver(message.SenderType);
        var recipientType = isFromCustomer ? UserTypes.Driver : UserTypes.Customer;
        var recipientId = isFromCustomer ? trip.DriverId : trip.CustomerId;
        if (recipientId is null || recipientId == Guid.Empty) return;

        var data = new Dictionary<string, string>
        {
            ["type"] = "messageReceived",
            ["tripId"] = trip.Id.ToString(),
            ["messageId"] = message.Id.ToString(),
            ["senderType"] = UserTypes.Canonical(message.SenderType),
            ["senderId"] = message.SenderId.ToString(),
            ["content"] = message.Content,
            ["sentAtUtc"] = message.SentAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };
        try
        {
            var unread = await _messages.CountUnreadByTripAsync(recipientType, recipientId.Value, ct);
            data["unreadTotal"] = unread.Sum(c => c.Count).ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            // The badge re-syncs on the recipient's next app resume; never fail a send over it.
        }

        _ = _push.NotifyUserAsync(
            recipientType,
            recipientId.Value,
            new PushMessage(
                isFromCustomer ? "Message from your passenger" : "Message from your driver",
                message.Content,
                data),
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
