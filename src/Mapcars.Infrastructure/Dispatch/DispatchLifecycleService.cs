using Mapcars.Application.Dispatch;
using Mapcars.Application.Dispatch.Interfaces;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Dispatch;

/// <summary>
/// The clock behind an open request. Two passes on one timer:
///
/// <list type="number">
/// <item><b>Escalate</b> — push a still-live request out to the wider ring as it
/// ages, per <see cref="DispatchRadius"/>.</item>
/// <item><b>Expire</b> — prompt the rider once its window closes, and close the
/// trip when the grace period runs out, per <see cref="TripExpiry"/>.</item>
/// </list>
///
/// They share a tick because they are the same question asked at two ages, and
/// splitting them would mean two timers walking the same small list.
///
/// **Neither pass is what enforces expiry.** A lapsed request is already invisible
/// to the board queries and already unacceptable to <c>TryAssignAsync</c>, both of
/// which compare against <c>ExpiresAtUtc</c> directly. This service only writes the
/// tombstone and tells people — so its 20s granularity costs a slightly late
/// notification, never a wrongly accepted trip. Keep it that way: moving the
/// enforcement here would make a driver's accept depend on when a timer last fired.
///
/// Re-broadcasts a trip only when its radius has actually *widened* since the last
/// push for that trip, so a request costs at most three broadcasts in its life
/// rather than one per tick. The bookkeeping is in memory and deliberately
/// disposable: after a restart the tables are empty, the next tick re-broadcasts
/// each open trip once at its current radius and re-prompts any paused rider, and
/// both clients de-duplicate (the board ignores a trip it already holds, and
/// <c>RequestAlerts</c> dedupes the buzz by trip id) — so the cost of forgetting is
/// a redundant push, not a double alert.
/// </summary>
public sealed class DispatchLifecycleService : BackgroundService
{
    /// <summary>
    /// Tick rate. Fine-grained enough that a 60s threshold is hit within ~20s
    /// of the mark, cheap enough to run against the open-trip list forever.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    /// <summary>Trip id → the radius it was last broadcast at.</summary>
    private readonly Dictionary<Guid, double> _lastRadius = new();

    /// <summary>
    /// Trip id → the deadline we last prompted the rider about. Keyed by deadline
    /// rather than by a bare "prompted" flag so an extended trip is prompted again
    /// when its *new* window lapses, instead of falling silent for the rest of its life.
    /// </summary>
    private readonly Dictionary<Guid, DateTime> _promptedFor = new();

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DispatchLifecycleService> _log;

    public DispatchLifecycleService(
        IServiceScopeFactory scopes, ILogger<DispatchLifecycleService> log)
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
                _log.LogWarning(ex, "Dispatch lifecycle sweep failed; will retry next interval.");
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
        var notifier = scope.ServiceProvider.GetRequiredService<ITripNotifier>();
        var push = scope.ServiceProvider.GetRequiredService<IPushService>();

        var now = DateTime.UtcNow;

        // Live requests only — ListAvailableAsync already hides lapsed ones.
        var live = await trips.ListAvailableAsync(ct);

        // Lapsed requests: both those still inside their grace period and those
        // past it. The complement of the list above, not a subset of it.
        var lapsed = await trips.ListLapsedAsync(now, ct);

        Forget(live, lapsed);

        await EscalateAsync(live, dispatch, now, ct);
        await ExpireAsync(lapsed, trips, dispatch, notifier, push, now, ct);
    }

    /// <summary>
    /// Drop bookkeeping for trips that are no longer open requests at all
    /// (accepted, cancelled, already expired) — otherwise these tables are a slow
    /// leak for the life of the process. Trips still inside their grace period are
    /// kept: an extension brings them straight back, and forgetting one would earn
    /// the rider a duplicate push twenty seconds later.
    /// </summary>
    private void Forget(IReadOnlyList<Trip> live, IReadOnlyList<Trip> lapsed)
    {
        var tracked = live.Select(t => t.Id).Concat(lapsed.Select(t => t.Id)).ToHashSet();

        foreach (var id in _lastRadius.Keys.Where(id => !tracked.Contains(id)).ToList())
            _lastRadius.Remove(id);

        foreach (var id in _promptedFor.Keys.Where(id => !tracked.Contains(id)).ToList())
            _promptedFor.Remove(id);
    }

    private async Task EscalateAsync(
        IReadOnlyList<Trip> live, IDispatchService dispatch, DateTime now, CancellationToken ct)
    {
        foreach (var trip in live)
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

    private async Task ExpireAsync(
        IReadOnlyList<Trip> lapsed,
        ITripRepository trips,
        IDispatchService dispatch,
        ITripNotifier notifier,
        IPushService push,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var trip in lapsed)
        {
            if (TripExpiry.IsPastGrace(trip, now))
                await CloseAsync(trip, trips, dispatch, notifier, push, now, ct);
            else
                await PromptAsync(trip, dispatch, notifier, push, ct);
        }
    }

    /// <summary>
    /// The window closed but the rider still has time to say "keep looking".
    /// Pull the request off the boards and ask them — once per window.
    /// </summary>
    private async Task PromptAsync(
        Trip trip,
        IDispatchService dispatch,
        ITripNotifier notifier,
        IPushService push,
        CancellationToken ct)
    {
        if (_promptedFor.TryGetValue(trip.Id, out var promptedFor)
            && promptedFor == trip.ExpiresAtUtc) return;

        _promptedFor[trip.Id] = trip.ExpiresAtUtc;

        // Off the boards. The queries already hide it, but a driver holding the
        // card needs telling, or it sits there until their next poll.
        await SafelyAsync(
            () => dispatch.WithdrawAsync(trip, DispatchWithdrawReason.Expired, ct),
            "withdraw lapsed trip {TripId} from driver boards", trip.Id);

        await SafelyAsync(
            () => notifier.TripExpiringAsync(trip.ToResponse(), ct),
            "push tripExpiring for trip {TripId}", trip.Id);

        // The one that actually matters: a rider whose app is in their pocket is
        // exactly the rider who otherwise loses the ride without being asked.
        await SafelyAsync(
            () => push.NotifyUserAsync("rider", trip.RiderId, new PushMessage(
                "Still looking for a driver",
                "Nobody has taken your ride yet. Tap to keep searching.",
                new Dictionary<string, string>
                {
                    ["type"] = "tripExpiring",
                    ["tripId"] = trip.Id.ToString(),
                }), ct),
            "push tripExpiring notification for trip {TripId}", trip.Id);

        _log.LogInformation(
            "Trip {TripId} lapsed after extension {Count} — rider prompted.",
            trip.Id, trip.ExtensionCount);
    }

    /// <summary>Grace is up: close the trip and tell the rider it's over.</summary>
    private async Task CloseAsync(
        Trip trip,
        ITripRepository trips,
        IDispatchService dispatch,
        ITripNotifier notifier,
        IPushService push,
        DateTime now,
        CancellationToken ct)
    {
        // Conditional update: returns false if a driver accepted in the moments
        // since this list was read, in which case there is nothing to close and
        // nothing to tell anyone.
        if (!await trips.TryExpireAsync(trip.Id, now, ct)) return;

        // The instance came from an AsNoTracking read, so it still says
        // "Requested". Bring it in line with the row we just wrote before it is
        // mapped for clients — nothing here is tracked, so nothing is persisted.
        trip.Status = TripStatus.Expired;
        trip.CancelledAtUtc = now;

        await SafelyAsync(
            () => dispatch.WithdrawAsync(trip, DispatchWithdrawReason.Expired, ct),
            "withdraw expired trip {TripId} from driver boards", trip.Id);

        await SafelyAsync(
            () => notifier.TripUpdatedAsync(trip.ToResponse(), ct),
            "push tripUpdated for expired trip {TripId}", trip.Id);

        await SafelyAsync(
            // No "tap to…" here. Neither app routes an FCM payload anywhere yet
            // (push_service only registers the token), so tapping just resumes
            // wherever the app was. That happens to land a backgrounded rider on
            // the prompt above, which is why the pause copy can say it — but
            // this one fires after the trip is closed, when there is nothing
            // left on that screen to tap.
            () => push.NotifyUserAsync("rider", trip.RiderId, new PushMessage(
                "No drivers found",
                "We couldn't find a driver for your ride. Book again when you're ready.",
                new Dictionary<string, string>
                {
                    ["type"] = "tripExpired",
                    ["tripId"] = trip.Id.ToString(),
                }), ct),
            "push tripExpired notification for trip {TripId}", trip.Id);

        _log.LogInformation(
            "Trip {TripId} expired after {Age:N0}s and {Count} extension(s).",
            trip.Id, (now - trip.CreatedAtUtc).TotalSeconds, trip.ExtensionCount);
    }

    /// <summary>
    /// Run a best-effort notification. One rider's dead device token or a SignalR
    /// hiccup must not abandon the rest of the sweep — the trips after this one in
    /// the list still need closing.
    /// </summary>
    private async Task SafelyAsync(Func<Task> action, string what, Guid tripId)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to " + what + ".", tripId);
        }
    }
}
