using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Trips.Interfaces;

public interface ITripRepository : IGenericRepository<Trip>
{
    Task<IReadOnlyList<Trip>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);
    Task<IReadOnlyList<Trip>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Unassigned, still-requested trips a driver could accept (no geo-matching — just all open requests).</summary>
    Task<IReadOnlyList<Trip>> ListAvailableAsync(CancellationToken ct = default);

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
