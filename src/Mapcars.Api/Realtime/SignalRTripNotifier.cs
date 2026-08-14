using Mapcars.Api.Hubs;
using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Mapcars.Api.Realtime;

/// <summary>
/// SignalR-backed <see cref="ITripNotifier"/>. Pushes to a trip's per-trip group
/// via <see cref="TripHub"/> (fanned across API instances by the Redis backplane).
/// Best-effort: swallows send failures so a realtime hiccup never fails the trip
/// action that triggered it.
/// </summary>
public sealed class SignalRTripNotifier : ITripNotifier
{
    private readonly IHubContext<TripHub> _hub;
    private readonly ILogger<SignalRTripNotifier> _log;

    public SignalRTripNotifier(IHubContext<TripHub> hub, ILogger<SignalRTripNotifier> log)
    {
        _hub = hub;
        _log = log;
    }

    public async Task TripUpdatedAsync(TripResponse trip, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(TripHub.GroupFor(trip.Id.ToString()))
                .SendAsync("tripUpdated", trip, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to push tripUpdated for trip {TripId}.", trip.Id);
        }
    }

    public async Task TripAvailableAsync(Guid driverId, TripResponse trip, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(TripHub.DriverGroupFor(driverId.ToString()))
                .SendAsync("tripAvailable", trip, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to push tripAvailable to driver {DriverId}.", driverId);
        }
    }

    public async Task TripTakenAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(TripHub.DriverGroupFor(driverId.ToString()))
                .SendAsync("tripTaken", tripId, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to push tripTaken to driver {DriverId}.", driverId);
        }
    }

    public async Task DriverLocationAsync(
        Guid tripId, double lat, double lng, double? heading = null, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(TripHub.GroupFor(tripId.ToString()))
                .SendAsync("driverLocation", new { lat, lng, heading }, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to push driverLocation for trip {TripId}.", tripId);
        }
    }

    public async Task MessageReceivedAsync(Guid tripId, MessageResponse message, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients
                .Group(TripHub.GroupFor(tripId.ToString()))
                .SendAsync("messageReceived", message, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to push messageReceived for trip {TripId}.", tripId);
        }
    }
}
