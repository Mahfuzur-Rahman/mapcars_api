using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.SavedPlaces.Dtos;
using Mapcars.Application.SavedPlaces.Interfaces;
using Mapcars.Application.SavedPlaces.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.SavedPlaces.Services;

/// <summary>
/// Business logic for a rider's saved places (Home/Work/custom). Every
/// operation is scoped to the calling rider — a rider can only ever read or
/// change their own saved places.
/// </summary>
public class SavedPlaceService : ISavedPlaceService
{
    private readonly ISavedPlaceRepository _places;
    private readonly IUnitOfWork _uow;

    public SavedPlaceService(ISavedPlaceRepository places, IUnitOfWork uow)
    {
        _places = places;
        _uow = uow;
    }

    public async Task<IReadOnlyList<SavedPlaceResponse>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
    {
        var places = await _places.ListForRiderAsync(riderId, ct);
        return places.Select(p => p.ToResponse()).ToList();
    }

    public async Task<SavedPlaceResponse> CreateAsync(Guid riderId, UpsertSavedPlaceRequest request, CancellationToken ct = default)
    {
        var label = request.Label.Trim();
        await EnsureLabelNotTaken(riderId, label, excludingPlaceId: null, ct);

        var place = new SavedPlace
        {
            RiderId = riderId,
            Label = label,
            Address = request.Address.Trim(),
            Lat = request.Lat,
            Lng = request.Lng,
        };

        await _places.AddAsync(place, ct);
        await _uow.SaveChangesAsync(ct);
        return place.ToResponse();
    }

    public async Task<SavedPlaceResponse> UpdateAsync(Guid riderId, Guid placeId, UpsertSavedPlaceRequest request, CancellationToken ct = default)
    {
        var place = await GetOwnedAsync(riderId, placeId, ct);

        var label = request.Label.Trim();
        await EnsureLabelNotTaken(riderId, label, excludingPlaceId: placeId, ct);

        place.Label = label;
        place.Address = request.Address.Trim();
        place.Lat = request.Lat;
        place.Lng = request.Lng;

        _places.Update(place);
        await _uow.SaveChangesAsync(ct);
        return place.ToResponse();
    }

    public async Task DeleteAsync(Guid riderId, Guid placeId, CancellationToken ct = default)
    {
        var place = await GetOwnedAsync(riderId, placeId, ct);
        _places.Remove(place);
        await _uow.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<SavedPlace> GetOwnedAsync(Guid riderId, Guid placeId, CancellationToken ct)
    {
        var place = await _places.GetByIdAsync(placeId, ct);
        if (place is null || place.RiderId != riderId)
            throw new NotFoundException("SavedPlace", placeId);
        return place;
    }

    private async Task EnsureLabelNotTaken(Guid riderId, string label, Guid? excludingPlaceId, CancellationToken ct)
    {
        var existing = await _places.ListForRiderAsync(riderId, ct);
        var clash = existing.Any(p =>
            p.Id != excludingPlaceId && string.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase));
        if (clash)
            throw new DomainException($"A saved place labelled '{label}' already exists.");
    }
}
