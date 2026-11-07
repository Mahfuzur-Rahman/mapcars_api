using Mapcars.Application.Messages.Dtos;

namespace Mapcars.Application.Messages.Interfaces;

/// <summary>The caller's Messages inbox. <c>callerType</c> is "customer" or "driver".</summary>
public interface IConversationService
{
    Task<IReadOnlyList<ConversationResponse>> ListAsync(string callerType, Guid callerId, CancellationToken ct = default);
    Task<ConversationResponse> GetAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
    Task<UnreadSummaryResponse> UnreadAsync(string callerType, Guid callerId, CancellationToken ct = default);
    Task MarkReadAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
}
