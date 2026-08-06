using Mapcars.Application.Geo.Models;

namespace Mapcars.Application.Geo.Interfaces;

/// <summary>
/// Live driver-location store — the hot path, backed by Redis GEO (implemented in
/// Infrastructure/Geo). All operations are best-effort: with Redis unavailable,
/// writes no-op and <see cref="QueryNearbyAsync"/> returns empty (matching is a
/// Redis-only feature, so it degrades to "no drivers" rather than failing).
/// </summary>
public interface IDriverLocationStore
{
    /// <summary>Add or move a driver in the live pool. <paramref name="heading"/>,
    /// when supplied, is stored alongside so nearby-car map markers can rotate to
    /// face the direction of travel — left null (leaving any prior value as-is)
    /// if the device couldn't determine one for this fix.</summary>
    Task UpsertAsync(Guid driverId, double lat, double lng, double? heading = null, CancellationToken ct = default);

    /// <summary>Remove a driver from the live pool (going offline).</summary>
    Task RemoveAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Online drivers within <paramref name="radiusMeters"/>, nearest first.</summary>
    Task<IReadOnlyList<NearbyDriver>> QueryNearbyAsync(
        double lat, double lng, double radiusMeters, int limit, CancellationToken ct = default);

    /// <summary>
    /// Belt-and-braces cleanup: removes any driver not heard from within the
    /// freshness window (e.g. the app crashed without calling
    /// <see cref="RemoveAsync"/>) so they don't linger in the set forever.
    /// <see cref="QueryNearbyAsync"/> already filters these out per-query — this
    /// just keeps the underlying set from growing unbounded. Returns the number
    /// pruned.
    /// </summary>
    Task<int> PruneStaleAsync(CancellationToken ct = default);
}
