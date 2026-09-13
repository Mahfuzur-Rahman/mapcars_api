using Mapcars.Application.Messages.Dtos;
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

    /// <summary>
    /// Tell a nearby driver a request they were shown has run out of time —
    /// pushed to their personal group as <c>tripExpired</c>.
    ///
    /// Deliberately not <c>tripTaken</c>: "someone beat you to it" and "nobody
    /// took it" want different copy and different card treatment, and collapsing
    /// them would tell drivers a job was taken when in fact none of them wanted
    /// it — which is exactly the signal a driver should be seeing.
    /// </summary>
    Task TripExpiredAsync(Guid driverId, Guid tripId, CancellationToken ct = default);

    /// <summary>
    /// Tell everyone tracking this trip that its search window has closed and
    /// the customer is being asked whether to keep looking (a <c>tripExpiring</c>
    /// event). The trip is still <c>Requested</c>, so no <c>tripUpdated</c>
    /// would otherwise fire.
    ///
    /// This is a backstop, not the trigger: a foregrounded customer app raises the
    /// prompt from its own countdown, so the prompt still appears on time when
    /// the realtime connection is dead — which is precisely when a customer would
    /// otherwise lose the ride without being asked.
    /// </summary>
    Task TripExpiringAsync(TripResponse trip, CancellationToken ct = default);

    /// <summary>Relay the assigned driver's live position to everyone tracking
    /// this trip (its per-trip group) — a <c>driverLocation</c> event.
    /// <paramref name="heading"/> (degrees, 0 = north, clockwise) is null when the
    /// device couldn't supply one; clients keep the marker's last known bearing
    /// rather than snapping it back to north.</summary>
    Task DriverLocationAsync(
        Guid tripId, double lat, double lng, double? heading = null, CancellationToken ct = default);

    /// <summary>Push a new chat message to everyone tracking this trip (its per-trip group).</summary>
    Task MessageReceivedAsync(Guid tripId, MessageResponse message, CancellationToken ct = default);
}
