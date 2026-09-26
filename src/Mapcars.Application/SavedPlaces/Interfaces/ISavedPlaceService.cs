using Mapcars.Application.SavedPlaces.Dtos;

namespace Mapcars.Application.SavedPlaces.Interfaces;

/// <summary>Saved-place use-cases (business logic layer surface). All operations are scoped to the calling customer.</summary>
public interface ISavedPlaceService
{
    Task<IReadOnlyList<SavedPlaceResponse>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<SavedPlaceResponse> CreateAsync(Guid customerId, UpsertSavedPlaceRequest request, CancellationToken ct = default);
    Task<SavedPlaceResponse> UpdateAsync(Guid customerId, Guid placeId, UpsertSavedPlaceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid customerId, Guid placeId, CancellationToken ct = default);
}
