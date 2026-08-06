using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Dtos;

namespace Mapcars.Application.Realtime;

/// <summary>
/// No-op notifier — the default so the Application layer resolves standalone (and
/// trip actions work even with realtime disabled). The API overrides this with a
/// SignalR-backed implementation.
/// </summary>
public sealed class NullTripNotifier : ITripNotifier
{
    public Task TripUpdatedAsync(TripResponse trip, CancellationToken ct = default) => Task.CompletedTask;

    public Task TripAvailableAsync(Guid driverId, TripResponse trip, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task TripTakenAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DriverLocationAsync(Guid tripId, double lat, double lng, CancellationToken ct = default)
        => Task.CompletedTask;
}
