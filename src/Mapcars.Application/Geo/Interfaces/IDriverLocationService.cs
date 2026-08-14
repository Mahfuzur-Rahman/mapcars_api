using Mapcars.Application.Geo.Dtos;

namespace Mapcars.Application.Geo.Interfaces;

/// <summary>Business rules over the live driver-location store (radius/limit clamps, mapping).</summary>
public interface IDriverLocationService
{
    Task UpdateAsync(Guid driverId, UpdateDriverLocationRequest req, CancellationToken ct = default);

    Task GoOfflineAsync(Guid driverId, CancellationToken ct = default);

    Task<IReadOnlyList<NearbyDriverResponse>> NearbyAsync(
        double lat, double lng, double? radiusMeters, int? limit, CancellationToken ct = default);

    /// <summary>
    /// The assigned driver's last known position for a trip the caller is a party
    /// to. Returns null when no driver is assigned yet, or when the driver isn't
    /// in the live pool (offline / never pushed / Redis unavailable). Throws
    /// <c>NotFoundException</c> if the caller isn't this trip's rider or driver.
    /// </summary>
    Task<TripDriverLocationResponse?> ForTripAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
}
