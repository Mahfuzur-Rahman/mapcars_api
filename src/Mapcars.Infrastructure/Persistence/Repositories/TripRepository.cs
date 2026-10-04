using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class TripRepository : GenericRepository<Trip>, ITripRepository
{
    // The partial unique indexes from 044_one_active_trip.sql: one live trip per
    // driver, one open trip per customer. A violation is matched by name, so these
    // must stay identical to the script's. Deliberately not mirrored in
    // TripConfiguration — EF would then drop its FK indexes on the same columns.
    private const string OneActiveTripPerDriverIndex = "uix_trips_one_active_per_driver";
    private const string OneOpenTripPerCustomerIndex = "uix_trips_one_open_per_customer";

    public TripRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Trip>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Trip>> ListForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Trip>> ListAvailableAsync(CancellationToken ct = default)
    {
        // Evaluated once, here, rather than inside the expression tree — EF would
        // otherwise translate DateTime.UtcNow to the *database* clock, and the
        // deadline this compares against was written from the API's.
        var now = DateTime.UtcNow;

        return await Set.AsNoTracking()
            .Where(t => t.Status == TripStatus.Requested
                        && t.DriverId == null
                        && t.ExpiresAtUtc > now)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Trip>> ListLapsedAsync(
        DateTime lapsedBeforeUtc, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.Status == TripStatus.Requested
                        && t.DriverId == null
                        && t.ExpiresAtUtc <= lapsedBeforeUtc)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<bool> TryExpireAsync(
        Guid tripId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Mirror of TryAssignAsync: one conditional UPDATE decides it, so an
        // accept landing in the same instant cannot also succeed.
        var rows = await Set
            .Where(t => t.Id == tripId
                        && t.Status == TripStatus.Requested
                        && t.DriverId == null
                        && t.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TripStatus.Expired)
                // Reuses the existing "when did this trip end" column rather
                // than adding an ExpiredAtUtc that would mean the same thing.
                // Status already says *how* it ended.
                .SetProperty(t => t.CancelledAtUtc, nowUtc)
                // Nobody accepted it, so nothing is owed. Safe to set flatly here
                // rather than conditionally: the WHERE above already restricts
                // this to a Requested trip with no driver, which cannot have been
                // settled.
                .SetProperty(t => t.PaymentStatus, PaymentStatus.Voided), ct);
        return rows == 1;
    }

    public async Task<decimal> GetUnpaidCardDebtAsync(Guid customerId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.CustomerId == customerId
                        && t.PaymentMethod == PaymentMethod.Card
                        && t.PaymentStatus == PaymentStatus.Failed
                        && t.PaymentWaivedAtUtc == null)
            .SumAsync(t => (decimal?)((t.FareAmount ?? 0m) + t.TipAmount), ct) ?? 0m;

    public Task<bool> HasActiveTripAsync(Guid driverId, CancellationToken ct = default)
        => Set.AsNoTracking().AnyAsync(
            t => t.DriverId == driverId &&
                 (t.Status == TripStatus.DriverAssigned ||
                  t.Status == TripStatus.DriverArrived ||
                  t.Status == TripStatus.InProgress),
            ct);

    public async Task<Trip?> GetActiveForCustomerAsync(Guid customerId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.CustomerId == customerId &&
                        (t.Status == TripStatus.Requested ||
                         t.Status == TripStatus.DriverAssigned ||
                         t.Status == TripStatus.DriverArrived ||
                         t.Status == TripStatus.InProgress))
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<Trip?> GetActiveForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.DriverId == driverId &&
                        (t.Status == TripStatus.DriverAssigned ||
                         t.Status == TripStatus.DriverArrived ||
                         t.Status == TripStatus.InProgress))
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    // NOTE: this calls Context.SaveChangesAsync, which commits the WHOLE unit of
    // work — every change pending on the context, not just this trip. Callers must
    // not have unrelated pending changes when they call it.
    public async Task<bool> TryAddRequestAsync(Trip trip, CancellationToken ct = default)
    {
        Set.Add(trip);
        try
        {
            await Context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, OneOpenTripPerCustomerIndex))
        {
            // Two bookings by one customer landed together and both passed the
            // service's open-trip check; the index let exactly one win. Detach the
            // loser's copy so the context stays usable.
            Context.Entry(trip).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<TripClaimResult> TryAssignAsync(Guid tripId, Guid driverId, CancellationToken ct = default)
    {
        // Single atomic UPDATE — only one caller can flip Requested→DriverAssigned.
        //
        // The ExpiresAtUtc clause is what actually stops a lapsed request being
        // accepted; the countdowns in both apps are advisory, since a phone's
        // clock is not something an assignment may depend on. It also settles
        // the accept-at-2:59.9 race by construction: this update and the
        // sweeper's cannot both match the same row.
        var now = DateTime.UtcNow;

        int rows;
        try
        {
            rows = await Set
                .Where(t => t.Id == tripId
                            && t.Status == TripStatus.Requested
                            && t.DriverId == null
                            && t.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, TripStatus.DriverAssigned)
                    .SetProperty(t => t.DriverId, driverId), ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex, OneActiveTripPerDriverIndex))
        {
            // Two accepts by one driver landed together and both passed the
            // service's HasActiveTripAsync check; the index let exactly one win.
            return TripClaimResult.DriverBusy;
        }

        if (rows != 1) return TripClaimResult.Unavailable;

        // ExecuteUpdate writes the row directly and never touches the change
        // tracker. The accept pre-checks loaded this trip through FindAsync, which
        // tracks it, so the context still holds it as Requested with no driver —
        // and GetByIdAsync would hand that stale copy straight back, into the
        // response, the customer's tripUpdated and the "driver on the way" push.
        var tracked = Set.Local.FindEntry(tripId);
        if (tracked is not null) await tracked.ReloadAsync(ct);

        return TripClaimResult.Assigned;
    }

    /// <summary>
    /// A unique violation on one specific index. Matched by name so that any
    /// other unique violation still surfaces as the bug it would be. ExecuteUpdate
    /// throws the provider exception bare; SaveChanges wraps it in a DbUpdateException.
    /// </summary>
    private static bool IsUniqueViolation(Exception ex, string indexName)
        => (ex as PostgresException ?? ex.InnerException as PostgresException)
               is { SqlState: PostgresErrorCodes.UniqueViolation } pg
           && pg.ConstraintName == indexName;

    public async Task<bool> TryStartChargeAsync(
        Guid tripId, DateTime nowUtc, TimeSpan staleAfter, CancellationToken ct = default)
    {
        // Same idiom as TryAssignAsync above: one conditional UPDATE decides it.
        // This is the whole answer to the dual-write hazard - the attempt fired
        // inline when the trip completed and the recovery sweeper both run this,
        // and exactly one can win.
        //
        // The staleness clause is the crash-recovery release. A process that set
        // the claim and then died leaves it standing; after `staleAfter` the
        // sweeper may take it, and re-attempting is safe because the same
        // idempotency key returns the intent the dead process created.
        var cutoff = nowUtc - staleAfter;

        var rows = await Set
            .Where(t => t.Id == tripId
                        && t.PaymentMethod == PaymentMethod.Card
                        && t.Status == TripStatus.Completed
                        // Failed and ActionRequired are claimable so a retry goes
                        // through the same gate. Collected, Refunded and Voided
                        // are terminal and must never be re-charged.
                        && (t.PaymentStatus == PaymentStatus.Processing
                         || t.PaymentStatus == PaymentStatus.Failed
                         || t.PaymentStatus == PaymentStatus.ActionRequired)
                        // An admin forgave this fare; do not chase it.
                        && t.PaymentWaivedAtUtc == null
                        && (t.ChargeStartedAtUtc == null || t.ChargeStartedAtUtc < cutoff))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ChargeStartedAtUtc, nowUtc)
                .SetProperty(t => t.PaymentStatus, PaymentStatus.Processing), ct);
        return rows == 1;
    }
}
