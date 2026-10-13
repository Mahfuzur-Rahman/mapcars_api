using System.Threading.Channels;
using Mapcars.Application.Trips.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// In-process queue for trip-completion emails, drained by this same hosted
/// service. In memory on purpose: a restart can drop a queued receipt, which is
/// cheaper than adding a durable outbox for a courtesy email — the receipt is
/// always available in the app. Bounded, so a mail outage can't grow it without
/// limit; when full the newest receipt is dropped and logged.
/// </summary>
public sealed class TripEmailQueue(IServiceScopeFactory scopes, ILogger<TripEmailQueue> log)
    : BackgroundService, ITripEmailQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(1000) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public void EnqueueCompleted(Guid tripId)
    {
        if (!_queue.Writer.TryWrite(tripId))
            log.LogWarning("Trip email queue full; completion emails for trip {TripId} were not sent.", tripId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var tripId in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // A scope per trip: the emailer's repositories and the email log
                    // share a DbContext, which must not outlive one unit of work.
                    using var scope = scopes.CreateScope();
                    var emailer = scope.ServiceProvider.GetRequiredService<ITripCompletionEmailer>();
                    await emailer.SendAsync(tripId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "Completion emails for trip {TripId} failed.", tripId);
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
