using Mapcars.Application.Pricing.Models;

namespace Mapcars.Application.Pricing.Interfaces;

/// <summary>
/// Persists and serves the current fare chart. The implementation keeps the chart
/// hot in process memory (invalidated across instances via Redis pub/sub), backed
/// by Redis and durably by Postgres. Reads are effectively free; writes publish an
/// invalidation so every API instance reloads.
/// </summary>
public interface IFareChartStore
{
    /// <summary>The current chart. Never null — seeds a default on first use.</summary>
    Task<FareChart> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Publishes a new chart: bumps the version, persists to Postgres + Redis, and
    /// broadcasts an invalidation. Returns the stored chart (with its new version).
    /// </summary>
    Task<FareChart> SetCurrentAsync(FareChart chart, CancellationToken ct = default);
}
