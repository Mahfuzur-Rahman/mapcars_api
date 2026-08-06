using Mapcars.Application.Geo.Dtos;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Geo.Services;

/// <summary>
/// Thin business layer over <see cref="IDriverLocationStore"/>: clamps the query
/// radius/limit to sane bounds and maps store results to API DTOs. The heavy
/// lifting (GEO indexing) lives in the Redis store.
/// </summary>
public class DriverLocationService : IDriverLocationService
{
    private const double DefaultRadiusMeters = 5_000;
    private const double MaxRadiusMeters = 50_000;
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    private static readonly TripStatus[] RelayableStatuses =
        [TripStatus.DriverAssigned, TripStatus.DriverArrived, TripStatus.InProgress];

    private readonly IDriverLocationStore _store;
    private readonly ITripRepository _trips;
    private readonly ITripNotifier _notifier;

    public DriverLocationService(IDriverLocationStore store, ITripRepository trips, ITripNotifier notifier)
    {
        _store = store;
        _trips = trips;
        _notifier = notifier;
    }

    public async Task UpdateAsync(Guid driverId, UpdateDriverLocationRequest req, CancellationToken ct = default)
    {
        await _store.UpsertAsync(driverId, req.Lat, req.Lng, req.Heading, ct);

        if (req.TripId is not { } tripId) return;

        // Never trust the client's tripId blindly — only relay if it really is
        // this driver's own active trip. Best-effort: a relay hiccup must never
        // fail the location push that keeps the driver in the live GEO pool.
        try
        {
            var trip = await _trips.GetByIdAsync(tripId, ct);
            if (trip is null || trip.DriverId != driverId) return;
            if (!RelayableStatuses.Contains(trip.Status)) return;

            await _notifier.DriverLocationAsync(tripId, req.Lat, req.Lng, ct);
        }
        catch
        {
            /* non-critical */
        }
    }

    public Task GoOfflineAsync(Guid driverId, CancellationToken ct = default)
        => _store.RemoveAsync(driverId, ct);

    public async Task<IReadOnlyList<NearbyDriverResponse>> NearbyAsync(
        double lat, double lng, double? radiusMeters, int? limit, CancellationToken ct = default)
    {
        var radius = Math.Clamp(radiusMeters ?? DefaultRadiusMeters, 1, MaxRadiusMeters);
        var cap = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var found = await _store.QueryNearbyAsync(lat, lng, radius, cap, ct);

        return found
            .Select(d => new NearbyDriverResponse(
                d.DriverId.ToString(), d.Lat, d.Lng, Math.Round(d.DistanceMeters, 1), d.Heading))
            .ToList();
    }
}
