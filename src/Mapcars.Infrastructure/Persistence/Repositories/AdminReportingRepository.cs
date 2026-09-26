using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <summary>EF Core reporting queries for the admin portal (read-only, no tracking).</summary>
public class AdminReportingRepository(AppDbContext db) : IAdminReportingRepository
{
    private static readonly TripStatus[] ActiveStatuses =
    {
        TripStatus.Requested, TripStatus.DriverAssigned,
        TripStatus.DriverArrived, TripStatus.InProgress,
    };

    public async Task<AdminStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var totalCustomers = await db.Customers.CountAsync(ct);
        var totalDrivers = await db.Drivers.CountAsync(ct);
        var onlineDrivers = await db.Drivers.CountAsync(d => d.IsOnline, ct);
        var pending = await db.Drivers.CountAsync(d => d.Status == DriverStatus.PendingApproval, ct);

        var activeTrips = await db.Trips.CountAsync(t => ActiveStatuses.Contains(t.Status), ct);
        var tripsToday = await db.Trips.CountAsync(t => t.CreatedAtUtc >= today, ct);
        var completedToday = await db.Trips.CountAsync(
            t => t.Status == TripStatus.Completed && t.CompletedAtUtc >= today, ct);
        var completed = db.Trips
            .Where(t => t.Status == TripStatus.Completed && t.CompletedAtUtc >= today);

        // Revenue is the commission, not the fare — the driver's share was never ours.
        var revenueToday = await completed.SumAsync(t => (decimal?)t.PlatformFeeAmount, ct) ?? 0m;
        var grossFaresToday = await completed.SumAsync(t => (decimal?)t.FareAmount, ct) ?? 0m;

        return new AdminStatsResponse(
            totalCustomers, totalDrivers, onlineDrivers, pending,
            activeTrips, tripsToday, completedToday, revenueToday, grossFaresToday);
    }

    public async Task<AdminTransactionsPage> ListTransactionsAsync(
        PaymentMethod? method, PaymentStatus? paymentStatus, DateTime? fromUtc, DateTime? toUtc,
        int skip, int take, CancellationToken ct = default)
    {
        // Completed only: nothing is owed on a cancelled or expired trip (those
        // are Voided), so they would be rows of zeros on a money page.
        var q = db.Trips.AsNoTracking().Where(t => t.Status == TripStatus.Completed);
        if (method.HasValue) q = q.Where(t => t.PaymentMethod == method.Value);
        if (paymentStatus.HasValue) q = q.Where(t => t.PaymentStatus == paymentStatus.Value);
        if (fromUtc.HasValue) q = q.Where(t => t.CompletedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(t => t.CompletedAtUtc < toUtc.Value);

        // Totals over the whole filtered set, as conditional sums in one query.
        var totals = await q
            .GroupBy(_ => 1)
            .Select(g => new AdminTransactionTotals(
                g.Count(),
                g.Sum(t => t.FareAmount ?? 0m),
                g.Sum(t => t.TipAmount),
                g.Sum(t => t.PlatformFeeAmount ?? 0m),
                g.Sum(t => t.DriverEarnings ?? 0m),
                g.Sum(t => t.PaymentMethod == PaymentMethod.Cash && t.PaymentStatus == PaymentStatus.Collected
                    ? (t.FareAmount ?? 0m) + t.TipAmount : 0m),
                g.Sum(t => t.PaymentMethod == PaymentMethod.Card && t.PaymentStatus == PaymentStatus.Collected
                    ? (t.FareAmount ?? 0m) + t.TipAmount : 0m),
                g.Sum(t => t.PaymentMethod == PaymentMethod.Card
                           && (t.PaymentStatus == PaymentStatus.Failed || t.PaymentStatus == PaymentStatus.ActionRequired)
                           && t.PaymentWaivedAtUtc == null
                    ? (t.FareAmount ?? 0m) + t.TipAmount : 0m)))
            .FirstOrDefaultAsync(ct)
            ?? new AdminTransactionTotals(0, 0m, 0m, 0m, 0m, 0m, 0m, 0m);

        var rows = await q
            .OrderByDescending(t => t.CompletedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(t => new
            {
                t.Id,
                t.CompletedAtUtc,
                CustomerName = t.Customer != null ? t.Customer.FullName : null,
                DriverName = t.Driver != null ? t.Driver.FullName : null,
                t.Tier,
                t.PaymentMethod,
                t.PaymentStatus,
                t.FareAmount,
                t.TipAmount,
                t.PlatformFeeAmount,
                t.DriverEarnings,
                t.PaidAtUtc,
                t.PaymentFailureCode,
                t.StripePaymentIntentId,
                t.PaymentWaivedAtUtc,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminTransactionItem(
                r.Id, r.CompletedAtUtc ?? default, r.CustomerName, r.DriverName, r.Tier,
                r.PaymentMethod.ToString(), r.PaymentStatus.ToString(),
                r.FareAmount ?? 0m, r.TipAmount, r.PlatformFeeAmount ?? 0m, r.DriverEarnings ?? 0m,
                r.PaidAtUtc, r.PaymentFailureCode, r.StripePaymentIntentId, r.PaymentWaivedAtUtc != null))
            .ToList();

        return new AdminTransactionsPage(items, totals, skip, take);
    }

    public async Task<AdminPayoutsPage> ListPayoutsAsync(
        PayoutStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var q = db.Payouts.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(p => p.Status == status.Value);

        var totals = await q
            .GroupBy(_ => 1)
            .Select(g => new AdminPayoutTotals(
                g.Count(),
                g.Sum(p => p.Status == PayoutStatus.Paid ? p.Amount : 0m),
                g.Sum(p => p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.InTransit ? p.Amount : 0m),
                g.Sum(p => p.Status == PayoutStatus.Failed ? p.Amount : 0m)))
            .FirstOrDefaultAsync(ct)
            ?? new AdminPayoutTotals(0, 0m, 0m, 0m);

        var rows = await q
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(p => new
            {
                p.Id,
                p.DriverId,
                DriverName = p.Driver != null ? p.Driver.FullName : null,
                p.Amount,
                p.Currency,
                p.Status,
                p.StripePayoutId,
                p.CreatedAtUtc,
                p.ArrivedAtUtc,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminPayoutItem(
                r.Id, r.DriverId, r.DriverName, r.Amount, r.Currency, r.Status.ToString(),
                r.StripePayoutId, r.CreatedAtUtc, r.ArrivedAtUtc))
            .ToList();

        return new AdminPayoutsPage(items, totals, skip, take);
    }

    public async Task<IReadOnlyList<AdminTripListItem>> ListTripsAsync(
        TripStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var q = db.Trips.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);

        // Project the enums as their stored form, then map to strings in memory
        // (EF can't translate enum .ToString() through the value converter).
        var rows = await q
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(t => new
            {
                t.Id,
                CustomerName = t.Customer != null ? t.Customer.FullName : null,
                DriverName = t.Driver != null ? t.Driver.FullName : null,
                t.PickupAddress,
                t.DropoffAddress,
                t.Status,
                t.Tier,
                t.FareAmount,
                t.TipAmount,
                t.PaymentMethod,
                t.PaymentStatus,
                t.CreatedAtUtc,
                t.CompletedAtUtc,
                t.CancelledAtUtc,
            })
            .ToListAsync(ct);

        return rows.Select(r => new AdminTripListItem(
            r.Id, r.CustomerName, r.DriverName, r.PickupAddress, r.DropoffAddress,
            r.Status.ToString(), r.Tier, r.FareAmount, r.TipAmount,
            r.PaymentMethod.ToString(), r.PaymentStatus.ToString(),
            r.CreatedAtUtc, r.CompletedAtUtc, r.CancelledAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<AdminActiveTrip>> ListActiveTripsAsync(CancellationToken ct = default)
    {
        var rows = await db.Trips.AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new
            {
                t.Id,
                t.Status,
                CustomerName = t.Customer != null ? t.Customer.FullName : null,
                DriverName = t.Driver != null ? t.Driver.FullName : null,
                t.PickupAddress,
                t.PickupLat,
                t.PickupLng,
                t.DropoffAddress,
                t.DropoffLat,
                t.DropoffLng,
            })
            .ToListAsync(ct);

        return rows.Select(r => new AdminActiveTrip(
            r.Id, r.Status.ToString(), r.CustomerName, r.DriverName,
            r.PickupAddress, r.PickupLat, r.PickupLng,
            r.DropoffAddress, r.DropoffLat, r.DropoffLng)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetDriverNamesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string?>();
        return await db.Drivers.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.FullName, ct);
    }
}
