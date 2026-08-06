namespace Mapcars.Application.Ratings.Dtos;

/// <summary>Submit a 1-5 star rating for a completed trip, in the caller's direction.</summary>
public record SubmitRatingRequest(int Score, string? Comment);

/// <summary>Outbound rating representation. Never expose the entity directly.</summary>
public record RatingResponse(
    Guid Id,
    Guid TripId,
    string RaterType,
    int Score,
    string? Comment,
    DateTime CreatedAtUtc);
