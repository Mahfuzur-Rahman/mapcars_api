using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class TripRepository : GenericRepository<Trip>, ITripRepository
{
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

    public async Task<bool> TryAssignAsync(Guid tripId, Guid driverId, CancellationToken ct = default)
    {
        // Single atomic UPDATE — only one caller can flip Requested→DriverAssigned.
        //
        // The ExpiresAtUtc clause is what actually stops a lapsed request being
        // accepted; the countdowns in both apps are advisory, since a phone's
        // clock is not something an assignment may depend on. It also settles
        // the accept-at-2:59.9 race by construction: this update and the
        // sweeper's cannot both match the same row.
        var now = DateTime.UtcNow;

        var rows = await Set
            .Where(t => t.Id == tripId
                        && t.Status == TripStatus.Requested
                        && t.DriverId == null
                        && t.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TripStatus.DriverAssigned)
                .SetProperty(t => t.DriverId, driverId), ct);
        return rows == 1;
    }

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
