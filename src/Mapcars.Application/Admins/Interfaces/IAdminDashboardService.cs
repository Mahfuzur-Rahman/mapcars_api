using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Admins.Interfaces;

/// <summary>Admin-portal reporting: dashboard stats, trip history, and the live map.</summary>
public interface IAdminDashboardService
{
    Task<AdminStatsResponse> GetStatsAsync(CancellationToken ct = default);

    /// <summary><paramref name="status"/> is a TripStatus name (case-insensitive); null/blank = all.</summary>
    Task<IReadOnlyList<AdminTripListItem>> ListTripsAsync(
        string? status, int skip, int take, CancellationToken ct = default);

    Task<AdminLiveResponse> GetLiveAsync(CancellationToken ct = default);
}
