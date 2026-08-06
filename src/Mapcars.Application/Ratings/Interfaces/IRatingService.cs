using Mapcars.Application.Ratings.Dtos;

namespace Mapcars.Application.Ratings.Interfaces;

/// <summary>Rating use-cases (business logic layer surface). <c>callerType</c> is "rider" or "driver".</summary>
public interface IRatingService
{
    Task<RatingResponse> SubmitAsync(
        string callerType, Guid callerId, Guid tripId, SubmitRatingRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<RatingResponse>> ListForTripAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
}
