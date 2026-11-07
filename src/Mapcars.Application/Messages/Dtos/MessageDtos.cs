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

/// <summary>The newest message in a conversation, for the list preview.</summary>
public record ConversationLastMessage(string Content, string SenderType, DateTime SentAtUtc);

/// <summary>One row of the caller's Messages list — one conversation per trip.</summary>
public record ConversationResponse(
    Guid TripId,
    string CounterpartName,
    string PickupAddress,
    string DropoffAddress,
    string TripStatus,
    DateTime TripDateUtc,
    ConversationLastMessage? LastMessage,
    int UnreadCount,
    bool CanSend);

public record UnreadTripCountResponse(Guid TripId, int Count);

/// <summary>Badge data: the total, plus per trip so in-trip buttons show their own count.</summary>
public record UnreadSummaryResponse(int Total, IReadOnlyList<UnreadTripCountResponse> Trips);
