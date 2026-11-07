using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Messages.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Messages.Mapping;

/// <summary>Manual entity ↔ DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class MessageMappings
{
    public static MessageResponse ToResponse(this TripMessage message) => new(
        message.Id,
        message.TripId,
        message.SenderType,
        message.SenderId,
        message.Content,
        message.SentAtUtc);

    /// <summary>Shapes a row for one viewer: the counterpart's name, the send rule.</summary>
    public static ConversationResponse ToResponse(
        this ConversationRow row, string viewerType, DateTime nowUtc, TimeSpan window)
    {
        var viewerIsDriver = UserTypes.IsDriver(viewerType);
        var name = viewerIsDriver ? row.CustomerName : row.DriverName;
        if (string.IsNullOrWhiteSpace(name)) name = viewerIsDriver ? "Passenger" : "Driver";

        var last = row.LastContent is null || row.LastSenderType is null || row.LastSentAtUtc is null
            ? null
            : new ConversationLastMessage(row.LastContent, UserTypes.Canonical(row.LastSenderType), row.LastSentAtUtc.Value);

        return new ConversationResponse(
            row.TripId,
            name,
            row.PickupAddress,
            row.DropoffAddress,
            row.Status.ToString(),
            row.TripCreatedAtUtc,
            last,
            row.UnreadCount,
            ChatPolicy.CanSend(row.Status, row.CompletedAtUtc, nowUtc, window));
    }
}
