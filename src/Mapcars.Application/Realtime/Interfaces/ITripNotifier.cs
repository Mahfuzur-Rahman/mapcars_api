using Mapcars.Application.Trips.Dtos;

namespace Mapcars.Application.Realtime.Interfaces;

/// <summary>
/// Pushes trip updates to connected clients in real time (implemented over
/// SignalR in the API layer). A <b>port</b> the Application depends on so it stays
/// free of any transport/SignalR reference. Best-effort: implementations must not
/// throw into the caller — a realtime hiccup should never fail a trip action.
/// </summary>
public interface ITripNotifier
{
    /// <summary>Broadcast the updated trip to everyone tracking it (its per-trip group).</summary>
    Task TripUpdatedAsync(TripResponse trip, CancellationToken ct = default);

    /// <summary>Tell a nearby driver about a new open request (broadcast model) —
    /// pushed to their personal group so it appears live on their requests board.</summary>
    Task TripAvailableAsync(Guid driverId, TripResponse trip, CancellationToken ct = default);

    /// <summary>Tell a nearby driver a request they may have been shown is no
    /// longer open (another driver accepted it, or it was cancelled before
    /// anyone did) — pushed to their personal group so it drops off their
    /// requests board.</summary>
    Task TripTakenAsync(Guid driverId, Guid tripId, CancellationToken ct = default);

    /// <summary>Relay the assigned driver's live position to everyone tracking
    /// this trip (its per-trip group) — a <c>driverLocation</c> event.</summary>
    Task DriverLocationAsync(Guid tripId, double lat, double lng, CancellationToken ct = default);
}
