using Mapcars.Application.Dispatch;
using Mapcars.Application.Dispatch.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Dispatch;

/// <summary>
/// Pushes an open request out to the wider ring as it ages, per
/// <see cref="DispatchRadius"/>.
///
/// Only the *push* needs this. The board poll already escalates on its own —
/// <c>ITripService.ListAvailableNearbyAsync</c> applies the same rule per
/// request, so a distant driver finds an aged job on their next poll regardless.
/// What the poll can't do is reach a driver whose phone is in their pocket
/// between jobs, which is exactly the driver a job going begging needs to wake.
///
/// Re-broadcasts a trip only when its radius has actually *widened* since the
/// last push for that trip, so a request costs at most three broadcasts in its
/// life rather than one per tick. The bookkeeping is in memory and deliberately
/// disposable: after a restart the table is empty, the next tick re-broadcasts
/// each open trip once at its current radius, and both clients de-duplicate
/// (the board ignores a trip it already holds, and <c>RequestAlerts</c> dedupes
/// the buzz by trip id) — so the cost of forgetting is a redundant push, not a
/// double alert.
/// </summary>
public sealed class DispatchEscalationService : BackgroundService
{
    /// <summary>
    /// Tick rate. Fine-grained enough that a 60s threshold is hit within ~20s
    /// of the mark, cheap enough to run against the open-trip list forever.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    /// <summary>Trip id → the radius it was last broadcast at.</summary>
    private readonly Dictionary<Guid, double> _lastRadius = new();

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DispatchEscalationService> _log;

    public DispatchEscalationService(
        IServiceScopeFactory scopes, ILogger<DispatchEscalationService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Dispatch escalation sweep failed; will retry next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        // Repositories and the dispatch service are scoped (they sit on the
        // DbContext), so a singleton hosted service has to open its own scope.
        using var scope = _scopes.CreateScope();
        var trips = scope.ServiceProvider.GetRequiredService<ITripRepository>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IDispatchService>();

        var open = await trips.ListAvailableAsync(ct);

        // Anything no longer open (accepted, cancelled) stops being tracked —
        // otherwise this table is a slow leak for the life of the process.
        var openIds = open.Select(t => t.Id).ToHashSet();
        foreach (var id in _lastRadius.Keys.Where(id => !openIds.Contains(id)).ToList())
            _lastRadius.Remove(id);

        var now = DateTime.UtcNow;
        foreach (var trip in open)
        {
            var radius = DispatchRadius.For(trip, now);

            // A trip we've never seen was already broadcast at the initial
            // radius by the booking itself — treat that as its baseline, or
            // every request would draw a second identical push (and a second
            // FCM banner, which nothing de-duplicates) 20s after booking.
            var last = _lastRadius.TryGetValue(trip.Id, out var seen)
                ? seen
                : DispatchRadius.InitialMeters;
            if (radius <= last) continue;

            await dispatch.BroadcastAsync(trip, radius, ct);
            _lastRadius[trip.Id] = radius;

            _log.LogInformation(
                "Trip {TripId} unaccepted after {Age:N0}s — re-broadcast at {Radius:N0}m.",
                trip.Id, (now - trip.CreatedAtUtc).TotalSeconds, radius);
        }
    }
}
