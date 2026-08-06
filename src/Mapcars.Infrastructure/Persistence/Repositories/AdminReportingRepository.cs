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

        var totalRiders = await db.Riders.CountAsync(ct);
        var totalDrivers = await db.Drivers.CountAsync(ct);
        var onlineDrivers = await db.Drivers.CountAsync(d => d.IsOnline, ct);
        var pending = await db.Drivers.CountAsync(d => d.Status == DriverStatus.PendingApproval, ct);

        var activeTrips = await db.Trips.CountAsync(t => ActiveStatuses.Contains(t.Status), ct);
        var tripsToday = await db.Trips.CountAsync(t => t.CreatedAtUtc >= today, ct);
        var completedToday = await db.Trips.CountAsync(
            t => t.Status == TripStatus.Completed && t.CompletedAtUtc >= today, ct);
        var revenueToday = await db.Trips
            .Where(t => t.Status == TripStatus.Completed && t.CompletedAtUtc >= today)
            .SumAsync(t => (decimal?)t.FareAmount, ct) ?? 0m;

        return new AdminStatsResponse(
            totalRiders, totalDrivers, onlineDrivers, pending,
            activeTrips, tripsToday, completedToday, revenueToday);
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
                RiderName = t.Rider != null ? t.Rider.FullName : null,
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
            r.Id, r.RiderName, r.DriverName, r.PickupAddress, r.DropoffAddress,
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
                RiderName = t.Rider != null ? t.Rider.FullName : null,
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
            r.Id, r.Status.ToString(), r.RiderName, r.DriverName,
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
