using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Trips.Dtos;

namespace Mapcars.Application.Trips.Interfaces;

/// <summary>Trip use-cases (business logic layer surface).</summary>
public interface ITripService
{
    Task<IReadOnlyList<TripResponse>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);
    Task<IReadOnlyList<TripResponse>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// All unassigned, still-requested trips (the broadcast board — every open
    /// request). Only visible to an admin-approved driver who is online.
    /// </summary>
    Task<IReadOnlyList<TripResponse>> ListAvailableAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// Open requests within <paramref name="radiusMeters"/> of a driver's point,
    /// nearest first. Same approval gate as <see cref="ListAvailableAsync"/>.
    /// </summary>
    Task<IReadOnlyList<TripResponse>> ListAvailableNearbyAsync(
        Guid driverId, double lat, double lng, double radiusMeters, CancellationToken ct = default);

    /// <summary>
    /// Books a trip for a rider. Prices the chosen tier authoritatively from the
    /// current fare chart, stores the fare breakdown + any tip, and broadcasts the
    /// open request to nearby drivers.
    /// </summary>
    Task<TripResponse> CreateAsync(Guid riderId, CreateTripRequest request, CancellationToken ct = default);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Accept an open trip (broadcast model, first-come — atomic).</summary>
    Task<TripResponse> AcceptAsync(Guid driverId, Guid tripId, CancellationToken ct = default);
    Task<TripResponse> ArriveAsync(Guid driverId, Guid tripId, CancellationToken ct = default);
    Task<TripResponse> StartAsync(Guid driverId, Guid tripId, CancellationToken ct = default);
    Task<TripResponse> CompleteAsync(Guid driverId, Guid tripId, CancellationToken ct = default);

    /// <summary>Cancel by either party. <paramref name="callerType"/> is "rider" or "driver".</summary>
    Task<TripResponse> CancelAsync(string callerType, Guid callerId, Guid tripId, CancelTripRequest request, CancellationToken ct = default);

    /// <summary>Fetch a single trip — only if the caller is its rider or assigned driver.</summary>
    Task<TripResponse> GetForUserAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct = default);
}
