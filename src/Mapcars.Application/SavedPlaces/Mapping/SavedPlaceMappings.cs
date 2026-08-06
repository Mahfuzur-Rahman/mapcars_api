using Mapcars.Application.SavedPlaces.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.SavedPlaces.Mapping;

/// <summary>Manual entity <-> DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class SavedPlaceMappings
{
    public static SavedPlaceResponse ToResponse(this SavedPlace place) => new(
        place.Id,
        place.Label,
        place.Address,
        place.Lat,
        place.Lng,
        place.CreatedAtUtc,
        place.UpdatedAtUtc);
}
