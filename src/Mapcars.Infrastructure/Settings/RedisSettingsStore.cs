using System.Collections.Concurrent;
using System.Text.Json;
using Mapcars.Application.Settings.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mapcars.Infrastructure.Settings;

/// <summary>
/// Key → document settings with the same three-tier read path as
/// <c>RedisFareChartStore</c>:
///   1. process memory — the hot path; both mobile apps hit the payment settings
///      on every launch
///   2. Redis — shared cache, survives a restart
///   3. Postgres — durable source of truth, survives a Redis flush
///
/// <para>
/// Writes persist to Postgres and Redis, then publish an invalidation carrying
/// the key so every instance drops just that entry. Redis is a pure accelerator:
/// every Redis call is best-effort and failure falls through to Postgres, so the
/// API keeps answering with Redis down.
/// </para>
///
/// <para>
/// Deliberately a near-transcription of the fare chart store rather than a
/// cleverer shared base class — the two differ only in being keyed, and keeping
/// them structurally identical means a fix to one is an obvious fix to the other.
/// The one real difference is the cache: a dictionary rather than a single field,
/// because this store holds many keys.
/// </para>
///
/// <para>
/// Registered as a singleton (it holds the cache and the Redis subscription), so
/// database access goes through a scope via <see cref="IServiceScopeFactory"/>.
/// </para>
/// </summary>
public sealed class RedisSettingsStore : ISettingsStore, IDisposable
{
    private const string RedisKeyPrefix = "settings:";
    private const string RedisChannelName = "settings:updated";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RedisSettingsStore> _log;
    private readonly IConnectionMultiplexer? _redis;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly ConcurrentDictionary<string, object> _cached = new();

    public RedisSettingsStore(
        IServiceScopeFactory scopes,
        ILogger<RedisSettingsStore> log,
        IConnectionMultiplexer? redis)
    {
        _scopes = scopes;
        _log = log;
        _redis = redis;
        TrySubscribeForInvalidation();
    }

    public async Task<T> GetAsync<T>(string key, CancellationToken ct = default) where T : class, new()
    {
        if (_cached.TryGetValue(key, out var hit) && hit is T typed) return typed;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached.TryGetValue(key, out var again) && again is T t) return t;
            var loaded = await LoadAsync<T>(key, ct);
            _cached[key] = loaded;
            return loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T> SetAsync<T>(
        string key, T value, Guid? updatedByAdminId = null, CancellationToken ct = default)
        where T : class, new()
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var latestVersion = await db.AppSettings
                .AsNoTracking()
                .Where(r => r.Key == key)
                .Select(r => (int?)r.Version)
                .OrderByDescending(v => v)
                .FirstOrDefaultAsync(ct) ?? 0;

            var payload = JsonSerializer.Serialize(value, Json);

            db.AppSettings.Add(new AppSettingRecord
            {
                Key = key,
                Version = latestVersion + 1,
                PayloadJson = payload,
                UpdatedByAdminId = updatedByAdminId,
            });
            await db.SaveChangesAsync(ct);

            await WriteRedisAsync(key, payload);
            await PublishInvalidationAsync(key);

            _cached[key] = value;
            _log.LogInformation("Published settings '{Key}' v{Version}.", key, latestVersion + 1);
            return value;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ─── load path ───────────────────────────────────────────────────────────

    private async Task<T> LoadAsync<T>(string key, CancellationToken ct) where T : class, new()
    {
        // 1. Redis (fast shared cache).
        var fromRedis = await ReadRedisAsync<T>(key);
        if (fromRedis is not null) return fromRedis;

        // 2. Postgres (durable). Also warm Redis for the next instance.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.AppSettings
            .AsNoTracking()
            .Where(r => r.Key == key)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(ct);

        if (record is not null)
        {
            var parsed = Deserialize<T>(record.PayloadJson);
            if (parsed is not null)
            {
                await WriteRedisAsync(key, record.PayloadJson);
                return parsed;
            }

            // Corrupt payload. Fall through to the defaults rather than throw: for
            // payment settings the type's own defaults are cash-only, which is the
            // safe direction to fail in. Log loudly - this should never happen.
            _log.LogError(
                "Settings '{Key}' v{Version} failed to deserialize; falling back to defaults.",
                key, record.Version);
            return new T();
        }

        // 3. Nothing anywhere - seed the type's defaults and persist them, so the
        //    row exists for an admin to edit rather than appearing out of nowhere.
        return await SeedDefaultAsync<T>(db, key, ct);
    }

    private async Task<T> SeedDefaultAsync<T>(AppDbContext db, string key, CancellationToken ct)
        where T : class, new()
    {
        var value = new T();
        var payload = JsonSerializer.Serialize(value, Json);

        db.AppSettings.Add(new AppSettingRecord { Key = key, Version = 1, PayloadJson = payload });
        await db.SaveChangesAsync(ct);

        await WriteRedisAsync(key, payload);
        _log.LogInformation("Seeded default settings '{Key}' v1.", key);
        return value;
    }

    // ─── Redis helpers (all best-effort) ──────────────────────────────────────

    private IDatabase? Db()
        => _redis is { IsConnected: true } ? _redis.GetDatabase() : null;

    private static string RedisKeyFor(string key) => $"{RedisKeyPrefix}{key}:current";

    private async Task<T?> ReadRedisAsync<T>(string key) where T : class, new()
    {
        try
        {
            var db = Db();
            if (db is null) return null;
            var value = await db.StringGetAsync(RedisKeyFor(key));
            return value.HasValue ? Deserialize<T>(value!) : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Settings '{Key}' Redis read failed; falling back to Postgres.", key);
            return null;
        }
    }

    private async Task WriteRedisAsync(string key, string payload)
    {
        try
        {
            var db = Db();
            if (db is not null) await db.StringSetAsync(RedisKeyFor(key), payload);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Settings '{Key}' Redis write failed (still persisted to Postgres).", key);
        }
    }

    private async Task PublishInvalidationAsync(string key)
    {
        try
        {
            if (_redis is { IsConnected: true })
                await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(RedisChannelName), key);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Settings '{Key}' invalidation publish failed.", key);
        }
    }

    private void TrySubscribeForInvalidation()
    {
        try
        {
            if (_redis is null) return;
            // Name the channel parameter rather than discarding it: a `_` here would
            // shadow the discard wanted by TryRemove(key, out _) below.
            _redis.GetSubscriber().Subscribe(RedisChannel.Literal(RedisChannelName), (channel, message) =>
            {
                var key = message.ToString();
                if (string.IsNullOrEmpty(key)) return;

                // Drop only the key that changed. The next GetAsync reloads it from
                // Redis, which the publisher has already written.
                _cached.TryRemove(key, out var _removed);
                _log.LogInformation("Settings '{Key}' invalidation received; dropped from cache.", key);
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not subscribe to the settings invalidation channel.");
        }
    }

    private static T? Deserialize<T>(string json) where T : class, new()
    {
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    public void Dispose() => _gate.Dispose();
}
