using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Drivers;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Geo.Dtos;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

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
    private readonly IDriverRepository _drivers;
    private readonly ITripRepository _trips;
    private readonly ITripNotifier _notifier;

    public DriverLocationService(
        IDriverLocationStore store,
        IDriverRepository drivers,
        ITripRepository trips,
        ITripNotifier notifier)
    {
        _store = store;
        _drivers = drivers;
        _trips = trips;
        _notifier = notifier;
    }

    public async Task UpdateAsync(Guid driverId, UpdateDriverLocationRequest req, CancellationToken ct = default)
    {
        // The GEO pool is the "available for work" pool: only an admin-approved,
        // online driver belongs in it. Anyone else is evicted rather than added,
        // so a client that keeps pushing can't put itself in front of riders.
        var driver = await _drivers.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        if (!DriverApproval.CanWork(driver))
        {
            await _store.RemoveAsync(driverId, ct);
            throw new DomainException(DriverApproval.BlockedMessage(driver.Status));
        }

        if (!driver.IsOnline)
        {
            await _store.RemoveAsync(driverId, ct);
            return;
        }

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

            await _notifier.DriverLocationAsync(tripId, req.Lat, req.Lng, req.Heading, ct);
        }
        catch
        {
            /* non-critical */
        }
    }

    public Task GoOfflineAsync(Guid driverId, CancellationToken ct = default)
        => _store.RemoveAsync(driverId, ct);

    public async Task<TripDriverLocationResponse?> ForTripAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        // Same rule as the trip endpoints: only this trip's two parties, and a
        // non-party gets a 404 rather than a 403 (don't confirm the trip exists).
        var isRider = callerType == "rider" && trip.RiderId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        if (!isRider && !isDriver) throw new NotFoundException("Trip", tripId);

        // Nothing to report before a driver is assigned, or once the trip is over
        // — a completed trip's driver is off on someone else's job by then, and
        // their position is no longer this rider's business.
        if (trip.DriverId is not { } driverId) return null;
        if (!RelayableStatuses.Contains(trip.Status)) return null;

        var pos = await _store.GetAsync(driverId, ct);
        if (pos is null) return null;

        var age = (int)Math.Max(0, (DateTime.UtcNow - pos.UpdatedAtUtc).TotalSeconds);
        return new TripDriverLocationResponse(pos.Lat, pos.Lng, pos.Heading, pos.UpdatedAtUtc, age);
    }

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
