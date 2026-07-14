using System.Text.Json;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing.Models;
using Mapcars.Domain.Entities;
using Mapcars.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mapcars.Infrastructure.Pricing;

/// <summary>
/// Fare chart store with a three-tier read path:
///   1. process memory  (<see cref="_cached"/>) — sub-microsecond, the hot path
///   2. Redis           — fast shared cache, keeps the chart across restarts
///   3. Postgres        — durable source of truth, survives a Redis flush
///
/// Writes persist to Postgres + Redis and publish an invalidation on a Redis
/// channel so every API instance drops its in-memory copy and reloads. Redis is a
/// pure accelerator: every Redis call is best-effort, and the store falls back to
/// Postgres (and finally the seed default) if Redis is unavailable, so the API
/// keeps pricing even with Redis down.
///
/// Registered as a singleton (holds the in-memory cache + Redis subscription), so
/// database access goes through a scope via <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed class RedisFareChartStore : IFareChartStore, IDisposable
{
    private const string RedisKey = "fare:chart:current";
    private const string RedisChannelName = "fare:chart:updated";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RedisFareChartStore> _log;
    private readonly IConnectionMultiplexer? _redis;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile FareChart? _cached;

    public RedisFareChartStore(
        IServiceScopeFactory scopes,
        ILogger<RedisFareChartStore> log,
        IConnectionMultiplexer? redis)
    {
        _scopes = scopes;
        _log = log;
        _redis = redis;
        TrySubscribeForInvalidation();
    }

    public async Task<FareChart> GetCurrentAsync(CancellationToken ct = default)
    {
        var cached = _cached;
        if (cached is not null) return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null) return _cached;
            _cached = await LoadAsync(ct);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<FareChart> SetCurrentAsync(FareChart chart, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var latestVersion = await db.FareCharts
                .AsNoTracking()
                .Select(r => (int?)r.Version)
                .OrderByDescending(v => v)
                .FirstOrDefaultAsync(ct) ?? 0;

            chart.Version = latestVersion + 1;
            chart.UpdatedAtUtc = DateTime.UtcNow;

            var payload = JsonSerializer.Serialize(chart, Json);

            db.FareCharts.Add(new FareChartRecord { Version = chart.Version, PayloadJson = payload });
            await db.SaveChangesAsync(ct);

            await WriteRedisAsync(payload);
            await PublishInvalidationAsync();

            _cached = chart;
            _log.LogInformation("Published fare chart v{Version}.", chart.Version);
            return chart;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ─── load path ───────────────────────────────────────────────────────────

    private async Task<FareChart> LoadAsync(CancellationToken ct)
    {
        // 1. Redis (fast shared cache).
        var fromRedis = await ReadRedisAsync();
        if (fromRedis is not null) return fromRedis;

        // 2. Postgres (durable). Also warm Redis for the next instance.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.FareCharts
            .AsNoTracking()
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(ct);

        if (record is not null)
        {
            var chart = Deserialize(record.PayloadJson);
            if (chart is not null)
            {
                await WriteRedisAsync(record.PayloadJson);
                return chart;
            }
            _log.LogWarning("Fare chart v{Version} in Postgres failed to deserialize; seeding default.", record.Version);
        }

        // 3. Nothing anywhere — seed the default and persist it.
        return await SeedDefaultAsync(db, ct);
    }

    private async Task<FareChart> SeedDefaultAsync(AppDbContext db, CancellationToken ct)
    {
        var chart = FareChartDefaults.Build();
        chart.Version = 1;
        chart.UpdatedAtUtc = DateTime.UtcNow;
        var payload = JsonSerializer.Serialize(chart, Json);

        db.FareCharts.Add(new FareChartRecord { Version = chart.Version, PayloadJson = payload });
        await db.SaveChangesAsync(ct);

        await WriteRedisAsync(payload);
        _log.LogInformation("Seeded default fare chart v1.");
        return chart;
    }

    // ─── Redis helpers (all best-effort) ───────────────────────────────────────

    private IDatabase? Db()
        => _redis is { IsConnected: true } ? _redis.GetDatabase() : null;

    private async Task<FareChart?> ReadRedisAsync()
    {
        try
        {
            var db = Db();
            if (db is null) return null;
            var value = await db.StringGetAsync(RedisKey);
            return value.HasValue ? Deserialize(value!) : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fare chart Redis read failed; falling back to Postgres.");
            return null;
        }
    }

    private async Task WriteRedisAsync(string payload)
    {
        try
        {
            var db = Db();
            if (db is not null) await db.StringSetAsync(RedisKey, payload);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fare chart Redis write failed (chart still persisted to Postgres).");
        }
    }

    private async Task PublishInvalidationAsync()
    {
        try
        {
            if (_redis is { IsConnected: true })
                await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(RedisChannelName), "reload");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fare chart invalidation publish failed.");
        }
    }

    private void TrySubscribeForInvalidation()
    {
        try
        {
            if (_redis is null) return;
            _redis.GetSubscriber().Subscribe(RedisChannel.Literal(RedisChannelName), (_, _) =>
            {
                _log.LogInformation("Fare chart invalidation received; dropping in-memory cache.");
                _cached = null; // next GetCurrentAsync reloads from Redis
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not subscribe to the fare chart invalidation channel.");
        }
    }

    private static FareChart? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<FareChart>(json, Json); }
        catch (JsonException) { return null; }
    }

    public void Dispose() => _gate.Dispose();
}
