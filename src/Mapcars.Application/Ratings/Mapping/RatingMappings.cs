using Mapcars.Application.Ratings.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Ratings.Mapping;

/// <summary>Manual entity <-> DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class RatingMappings
{
    public static RatingResponse ToResponse(this Rating rating) => new(
        rating.Id,
        rating.TripId,
        rating.RaterType,
        rating.Score,
        rating.Comment,
        rating.CreatedAtUtc);
}
