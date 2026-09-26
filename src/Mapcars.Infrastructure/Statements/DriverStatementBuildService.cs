using Mapcars.Application.Statements.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Statements;

/// <summary>
/// Builds weekly driver statements. Hourly, because it is idempotent and cheap
/// when there's nothing to do — so there is no schedule to get wrong, no "the job
/// was down at 01:00 on Monday" failure mode, and a restart mid-week costs nothing.
/// Several API instances may all run it; the unique indexes in database/041 let
/// exactly one insert each statement.
/// </summary>
public sealed class DriverStatementBuildService(
    IServiceScopeFactory scopes, ILogger<DriverStatementBuildService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Let startup finish (migrations checks, warm-up) before the first run.</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDriverStatementService>();
                var created = await service.BuildRecentAsync(DateTime.UtcNow, stoppingToken);
                if (created > 0) log.LogInformation("Built {Count} driver statement(s).", created);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Driver statement build failed; will retry next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
