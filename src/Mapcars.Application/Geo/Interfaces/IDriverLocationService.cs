using Mapcars.Application.Geo.Dtos;

namespace Mapcars.Application.Geo.Interfaces;

/// <summary>Business rules over the live driver-location store (radius/limit clamps, mapping).</summary>
public interface IDriverLocationService
{
    Task UpdateAsync(Guid driverId, UpdateDriverLocationRequest req, CancellationToken ct = default);

    Task GoOfflineAsync(Guid driverId, CancellationToken ct = default);

    Task<IReadOnlyList<NearbyDriverResponse>> NearbyAsync(
        double lat, double lng, double? radiusMeters, int? limit, CancellationToken ct = default);
}
