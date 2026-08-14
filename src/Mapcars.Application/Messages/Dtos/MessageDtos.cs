namespace Mapcars.Application.Messages.Dtos;

/// <summary>Send a chat message during an active trip.</summary>
public record SendMessageRequest(string Content);

/// <summary>Outbound message representation. Never expose the entity directly.</summary>
public record MessageResponse(
    Guid Id,
    Guid TripId,
    string SenderType,
    Guid SenderId,
    string Content,
    DateTime SentAtUtc);
