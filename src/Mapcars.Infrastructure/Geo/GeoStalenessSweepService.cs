using Mapcars.Application.Geo.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Geo;

/// <summary>
/// Periodically prunes stale entries from the live driver-location GEO set —
/// belt-and-braces beyond <see cref="IDriverLocationStore.QueryNearbyAsync"/>'s
/// per-query freshness filter, which hides stale drivers from results but
/// doesn't stop the underlying set from growing unbounded if a driver's app
/// crashes without calling <c>DELETE /drivers/location</c>.
/// </summary>
public sealed class GeoStalenessSweepService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IDriverLocationStore _locations;
    private readonly ILogger<GeoStalenessSweepService> _log;

    public GeoStalenessSweepService(IDriverLocationStore locations, ILogger<GeoStalenessSweepService> log)
    {
        _locations = locations;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var pruned = await _locations.PruneStaleAsync(stoppingToken);
                if (pruned > 0)
                    _log.LogInformation("Geo staleness sweep pruned {Count} stale driver location(s).", pruned);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Geo staleness sweep failed; will retry next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
