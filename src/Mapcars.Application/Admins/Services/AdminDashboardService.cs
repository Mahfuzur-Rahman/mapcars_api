using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Admins.Services;

/// <summary>
/// Admin reporting service. Stats/trips come from the reporting repository
/// (Postgres); live online-driver positions come from the Redis GEO store (best
/// effort — an empty pool just means no driver dots, never an error).
/// </summary>
public class AdminDashboardService(
    IAdminReportingRepository reporting,
    IDriverLocationStore locations) : IAdminDashboardService
{
    // Central London — the live pool is city-scoped, so a wide radius around the
    // centre effectively returns every online driver.
    private const double LondonLat = 51.5074;
    private const double LondonLng = -0.1278;
    private const double CityRadiusMeters = 100_000; // 100 km
    private const int MaxOnlineDrivers = 500;

    public Task<AdminStatsResponse> GetStatsAsync(CancellationToken ct = default)
        => reporting.GetStatsAsync(ct);

    public Task<IReadOnlyList<AdminTripListItem>> ListTripsAsync(
        string? status, int skip, int take, CancellationToken ct = default)
    {
        // The admin portal sends whatever spelling its build knows. A client
        // older than the rename sends "CancelledByRider", which no longer parses
        // - and an unparsed filter falls through to null, which does not show
        // NOTHING, it shows EVERY trip. Silently wrong in the least obvious
        // direction, so translate before parsing. Remove once the portal has
        // shipped the renamed value.
        if (string.Equals(status, "CancelledByRider", StringComparison.OrdinalIgnoreCase))
            status = nameof(TripStatus.CancelledByCustomer);

        TripStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<TripStatus>(status, ignoreCase: true, out var s))
            parsed = s;

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);
        return reporting.ListTripsAsync(parsed, skip, take, ct);
    }

    public async Task<AdminLiveResponse> GetLiveAsync(CancellationToken ct = default)
    {
        var activeTrips = await reporting.ListActiveTripsAsync(ct);

        var nearby = await locations.QueryNearbyAsync(
            LondonLat, LondonLng, CityRadiusMeters, MaxOnlineDrivers, ct);

        var names = nearby.Count == 0
            ? new Dictionary<Guid, string?>()
            : await reporting.GetDriverNamesAsync(nearby.Select(d => d.DriverId).ToList(), ct);

        var onlineDrivers = nearby
            .Select(d => new AdminOnlineDriver(
                d.DriverId,
                names.TryGetValue(d.DriverId, out var name) ? name : null,
                d.Lat, d.Lng, d.Heading))
            .ToList();

        return new AdminLiveResponse(activeTrips, onlineDrivers);
    }
}
