using Mapcars.Application.Messages.Dtos;
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
}
