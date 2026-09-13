using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Trips.Interfaces;

public interface ITripRepository : IGenericRepository<Trip>
{
    Task<IReadOnlyList<Trip>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);
    Task<IReadOnlyList<Trip>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// Unassigned, still-requested trips a driver could accept (no geo-matching
    /// — just all open requests). Excludes requests whose search window has
    /// closed: a lapsed request is unacceptable, so showing it on a board would
    /// only offer a driver a card that 400s when tapped.
    /// </summary>
    Task<IReadOnlyList<Trip>> ListAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Open requests whose window closed at or before <paramref name="lapsedBeforeUtc"/>
    /// — the sweeper's input, and deliberately the complement of
    /// <see cref="ListAvailableAsync"/> rather than a filter on it.
    /// </summary>
    Task<IReadOnlyList<Trip>> ListLapsedAsync(DateTime lapsedBeforeUtc, CancellationToken ct = default);

    /// <summary>
    /// Atomically close a lapsed request (Requested + still unassigned + window
    /// closed → Expired). Returns false if a driver accepted it first.
    ///
    /// Atomic for the same reason <see cref="TryAssignAsync"/> is: the sweeper
    /// and an accept can land in the same instant, and a read-then-write here
    /// would let the sweeper expire a trip a driver is already driving to.
    /// </summary>
    Task<bool> TryExpireAsync(Guid tripId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>True if the driver is on a live trip (assigned / arrived / in-progress) — i.e. not free to dispatch.</summary>
    Task<bool> HasActiveTripAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Fetch the single active trip for a rider, or null if none.</summary>
    Task<Trip?> GetActiveForRiderAsync(Guid riderId, CancellationToken ct = default);

    /// <summary>Fetch the single active trip for a driver, or null if none.</summary>
    Task<Trip?> GetActiveForDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// Atomically assign a still-open trip to a driver (Requested + unassigned →
    /// DriverAssigned). Returns false if it was already taken/cancelled — the
    /// single guard against two drivers grabbing the same trip.
    /// </summary>
    Task<bool> TryAssignAsync(Guid tripId, Guid driverId, CancellationToken ct = default);
}
