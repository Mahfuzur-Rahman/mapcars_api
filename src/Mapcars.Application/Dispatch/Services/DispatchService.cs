using Mapcars.Application.Dispatch.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Mapping;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Dispatch.Services;

/// <summary>
/// Broadcast dispatch: when a trip is booked, push it to every nearby online,
/// approved, free driver with a compatible vehicle tier so it appears on their board in real time.
/// </summary>
public class DispatchService : IDispatchService
{
    private const double BroadcastRadiusMeters = 10_000; // 10 km around the pickup
    private const int MaxDrivers = 50;

    private readonly IDriverLocationStore _locations;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly ITripRepository _trips;
    private readonly ITripNotifier _notifier;

    public DispatchService(
        IDriverLocationStore locations,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        ITripRepository trips,
        ITripNotifier notifier)
    {
        _locations = locations;
        _drivers = drivers;
        _vehicles = vehicles;
        _trips = trips;
        _notifier = notifier;
    }

    public async Task BroadcastAsync(Trip trip, CancellationToken ct = default)
    {
        var nearby = await _locations.QueryNearbyAsync(
            trip.PickupLat, trip.PickupLng, BroadcastRadiusMeters, MaxDrivers, ct);
        if (nearby.Count == 0) return;

        var response = trip.ToResponse();
        foreach (var candidate in nearby)
        {
            var driver = await _drivers.GetByIdAsync(candidate.DriverId, ct);
            if (driver is null || driver.Status != DriverStatus.Approved || !driver.IsOnline) continue;
            if (await _trips.HasActiveTripAsync(candidate.DriverId, ct)) continue; // don't ping busy drivers

            var vehicle = await _vehicles.GetByDriverAsync(candidate.DriverId, ct);
            if (vehicle is not null && !IsTierCompatible(vehicle.Tier, trip.Tier)) continue;

            await _notifier.TripAvailableAsync(candidate.DriverId, response, ct);
        }
    }

    public async Task WithdrawAsync(Trip trip, CancellationToken ct = default)
    {
        var nearby = await _locations.QueryNearbyAsync(
            trip.PickupLat, trip.PickupLng, BroadcastRadiusMeters, MaxDrivers, ct);
        foreach (var candidate in nearby)
            await _notifier.TripTakenAsync(candidate.DriverId, trip.Id, ct);
    }

    public static bool IsTierCompatible(string? driverTier, string? tripTier)
    {
        if (string.IsNullOrWhiteSpace(tripTier)) return true;
        var dTier = (driverTier ?? "economy").ToLowerInvariant();
        var tTier = tripTier.ToLowerInvariant();

        if (dTier == tTier) return true;

        return (dTier, tTier) switch
        {
            ("premium", "comfort" or "economy") => true,
            ("xl", "comfort" or "economy") => true,
            ("comfort", "economy") => true,
            _ => false
        };
    }
}
