using Mapcars.Application.Messages.Dtos;

namespace Mapcars.Application.Messages.Interfaces;

/// <summary>Message use-cases (business logic layer surface). <c>callerType</c> is "rider" or "driver".</summary>
public interface IMessageService
{
    Task<MessageResponse> SendAsync(
        string callerType, Guid callerId, Guid tripId, SendMessageRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MessageResponse>> ListForTripAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
}
