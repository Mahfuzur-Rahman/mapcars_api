namespace Mapcars.Application.SavedPlaces.Dtos;

/// <summary>Create-or-update one of the authenticated rider's saved places.</summary>
public record UpsertSavedPlaceRequest(
    string Label,
    string Address,
    double Lat,
    double Lng);

/// <summary>Outbound saved-place representation. Never expose the entity directly.</summary>
public record SavedPlaceResponse(
    Guid Id,
    string Label,
    string Address,
    double Lat,
    double Lng,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
