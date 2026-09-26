using Mapcars.Application.Admins.Dtos;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Admins.Interfaces;

/// <summary>
/// Read-only reporting queries for the admin portal (dashboard stats, trip
/// history, live-map active trips). Aggregate/join queries live here rather than
/// on the per-entity repositories so the Application layer stays EF-free.
/// </summary>
public interface IAdminReportingRepository
{
    Task<AdminStatsResponse> GetStatsAsync(CancellationToken ct = default);

    /// <summary>Most-recent-first page of trips, optionally filtered by status.</summary>
    Task<IReadOnlyList<AdminTripListItem>> ListTripsAsync(
        TripStatus? status, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Completed trips (the only ones money moves on), most recent first, filtered
    /// by method / payment status / completion window, with totals over the whole
    /// filtered set.
    /// </summary>
    Task<AdminTransactionsPage> ListTransactionsAsync(
        PaymentMethod? method, PaymentStatus? paymentStatus, DateTime? fromUtc, DateTime? toUtc,
        int skip, int take, CancellationToken ct = default);

    /// <summary>Driver payouts, most recent first, optionally filtered by status.</summary>
    Task<AdminPayoutsPage> ListPayoutsAsync(
        PayoutStatus? status, int skip, int take, CancellationToken ct = default);

    /// <summary>In-flight trips (requested / assigned / arrived / in-progress) for the live map.</summary>
    Task<IReadOnlyList<AdminActiveTrip>> ListActiveTripsAsync(CancellationToken ct = default);

    /// <summary>Resolve driver display names for a set of ids (for live-map markers).</summary>
    Task<IReadOnlyDictionary<Guid, string?>> GetDriverNamesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
