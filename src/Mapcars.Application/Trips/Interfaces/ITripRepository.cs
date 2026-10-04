using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Trips.Interfaces;

/// <summary>How an attempt to claim an open request for a driver came out.</summary>
public enum TripClaimResult
{
    /// <summary>The trip is now DriverAssigned to this driver.</summary>
    Assigned,

    /// <summary>Someone else took it, it was cancelled, or its window closed.</summary>
    Unavailable,

    /// <summary>
    /// The driver is already on a live trip. Normally caught before the claim;
    /// this is the race where two accepts by one driver land together.
    /// </summary>
    DriverBusy
}

public interface ITripRepository : IGenericRepository<Trip>
{
    Task<IReadOnlyList<Trip>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
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

    /// <summary>
    /// What this customer owes: card fares that failed and were never recovered or
    /// waived, in GBP (fare + tip). The input to the booking debt block.
    /// </summary>
    Task<decimal> GetUnpaidCardDebtAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>True if the driver is on a live trip (assigned / arrived / in-progress) — i.e. not free to dispatch.</summary>
    Task<bool> HasActiveTripAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Fetch the single active trip for a customer, or null if none.</summary>
    Task<Trip?> GetActiveForCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Fetch the single active trip for a driver, or null if none.</summary>
    Task<Trip?> GetActiveForDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// Add a new open request and save it. Returns false — with nothing saved —
    /// if the customer already has an open trip: the race where two bookings by
    /// one customer land together, past the service's own check.
    /// </summary>
    Task<bool> TryAddRequestAsync(Trip trip, CancellationToken ct = default);

    /// <summary>
    /// Atomically assign a still-open trip to a driver (Requested + unassigned →
    /// DriverAssigned) — the single guard against two drivers grabbing the same
    /// trip, and against one driver taking two at once.
    ///
    /// <para>
    /// On <see cref="TripClaimResult.Assigned"/>, any copy of the trip this unit
    /// of work already holds is refreshed from the row, so a following
    /// <c>GetByIdAsync</c> sees it assigned rather than as it was before the claim.
    /// </para>
    /// </summary>
    Task<TripClaimResult> TryAssignAsync(Guid tripId, Guid driverId, CancellationToken ct = default);

    /// <summary>
    /// Claim the exclusive right to charge this trip. Returns false if someone
    /// else holds the claim — the loser walks away rather than charging too.
    ///
    /// <para>
    /// <paramref name="staleAfter"/> is the crash-recovery release: a process
    /// that took the claim and then died leaves it behind, and after this long
    /// the sweeper may take it. Re-attempting is safe because it reuses the same
    /// idempotency key, so the provider returns the intent the dead process
    /// created rather than making a second one.
    /// </para>
    /// </summary>
    Task<bool> TryStartChargeAsync(
        Guid tripId, DateTime nowUtc, TimeSpan staleAfter, CancellationToken ct = default);
}
