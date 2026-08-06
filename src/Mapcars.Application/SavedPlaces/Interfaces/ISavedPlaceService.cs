using Mapcars.Application.SavedPlaces.Dtos;

namespace Mapcars.Application.SavedPlaces.Interfaces;

/// <summary>Saved-place use-cases (business logic layer surface). All operations are scoped to the calling rider.</summary>
public interface ISavedPlaceService
{
    Task<IReadOnlyList<SavedPlaceResponse>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);
    Task<SavedPlaceResponse> CreateAsync(Guid riderId, UpsertSavedPlaceRequest request, CancellationToken ct = default);
    Task<SavedPlaceResponse> UpdateAsync(Guid riderId, Guid placeId, UpsertSavedPlaceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid riderId, Guid placeId, CancellationToken ct = default);
}
