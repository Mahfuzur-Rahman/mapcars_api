using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Trips.Dtos;

namespace Mapcars.Application.Trips.Interfaces;

/// <summary>Trip use-cases (business logic layer surface).</summary>
public interface ITripService
{
    Task<IReadOnlyList<TripResponse>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<TripResponse>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// All unassigned, still-requested trips (the broadcast board — every open
    /// request). Only visible to an admin-approved driver who is online.
    /// </summary>
    Task<IReadOnlyList<TripResponse>> ListAvailableAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// Open requests in reach of a driver's point, nearest first. Same approval
    /// gate as <see cref="ListAvailableAsync"/>.
    ///
    /// "In reach" is each request's own escalating radius (see
    /// <c>DispatchRadius</c>), not one number for the board — a request widens
    /// its reach the longer it goes unaccepted. <paramref name="radiusMeters"/>
    /// is an optional extra cap for a caller that wants a narrower view; it can
    /// only ever shrink the result, never widen it past the dispatch rule.
    /// </summary>
    Task<IReadOnlyList<TripResponse>> ListAvailableNearbyAsync(
        Guid driverId, double lat, double lng, double? radiusMeters = null, CancellationToken ct = default);

    /// <summary>
    /// Books a trip for a customer. Prices the chosen tier authoritatively from the
    /// current fare chart, stores the fare breakdown + any tip, and broadcasts the
    /// open request to nearby drivers.
    /// </summary>
    Task<TripResponse> CreateAsync(Guid customerId, CreateTripRequest request, CancellationToken ct = default);

    /// <summary>
    /// Give a still-open request another search window (see <c>TripExpiry</c>)
    /// and put it back in front of drivers at the widest radius. The customer's
    /// answer to "nobody has taken this yet — keep looking?".
    ///
    /// Refused once the trip has left <c>Requested</c>, once the customer has used
    /// their extensions, or once the grace period has run out — a request the
    /// customer has been told is over must not come back.
    /// </summary>
    Task<TripResponse> ExtendAsync(Guid customerId, Guid tripId, CancellationToken ct = default);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Accept an open trip (broadcast model, first-come — atomic).</summary>
    Task<TripResponse> AcceptAsync(Guid driverId, Guid tripId, CancellationToken ct = default);
    Task<TripResponse> ArriveAsync(Guid driverId, Guid tripId, CancellationToken ct = default);
    /// <summary>
    /// Start the trip. <paramref name="pin"/> is the code the passenger reads out
    /// at the kerb.
    ///
    /// <para>
    /// OPTIONAL for now, deliberately: a driver build older than this change
    /// sends nothing, and refusing those would strand live trips mid-shift. A
    /// correct PIN is recorded as proof; a WRONG one is rejected outright. Make
    /// it required once every driver build sends it.
    /// </para>
    /// </summary>
    Task<TripResponse> StartAsync(
        Guid driverId, Guid tripId, string? pin = null, CancellationToken ct = default);
    Task<TripResponse> CompleteAsync(Guid driverId, Guid tripId, CancellationToken ct = default);

    /// <summary>Cancel by either party. <paramref name="callerType"/> is "customer" or "driver".</summary>
    Task<TripResponse> CancelAsync(string callerType, Guid callerId, Guid tripId, CancelTripRequest request, CancellationToken ct = default);

    /// <summary>Fetch a single trip — only if the caller is its customer or assigned driver.</summary>
    Task<TripResponse> GetForUserAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);

    /// <summary>Fetch the currently active trip for the caller (customer or driver), or null if none.</summary>
    Task<TripResponse?> GetActiveForUserAsync(string callerType, Guid callerId, CancellationToken ct = default);

    /// <summary>Fetch the authoritative receipt details for a trip.</summary>
    Task<TripReceiptResponse> GetReceiptForUserAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
}
