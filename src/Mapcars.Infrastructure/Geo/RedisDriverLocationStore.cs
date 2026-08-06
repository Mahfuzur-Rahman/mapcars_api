using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Geo.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Mapcars.Infrastructure.Geo;

/// <summary>
/// Live driver locations on <b>Redis GEO</b> — the matching hot path (never
/// Postgres). Each online driver is one member of a GEO set keyed by driver id;
/// a companion sorted set tracks each driver's last-seen time so a crashed driver
/// (whose app stopped pushing) is filtered out of nearby results rather than
/// lingering as a "ghost". A second companion hash carries the driver's last
/// known compass heading (GEO itself has no room for extra fields).
///
/// Registered as a singleton with an optional multiplexer (null / disconnected →
/// every op is a safe no-op, and nearby returns empty). Provision a real Redis for
/// matching to actually work — see TODO.md / FARE_CALCULATION.md's Redis note.
/// </summary>
public sealed class RedisDriverLocationStore : IDriverLocationStore
{
    private const string GeoKey = "geo:drivers:online";
    private const string SeenKey = "geo:drivers:seen";
    private const string HeadingKey = "geo:drivers:heading";
    private const int FreshnessSeconds = 60; // drop drivers not heard from within this window

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisDriverLocationStore> _log;

    public RedisDriverLocationStore(IConnectionMultiplexer? redis, ILogger<RedisDriverLocationStore> log)
    {
        _redis = redis;
        _log = log;
    }

    private IDatabase? Db() => _redis is { IsConnected: true } ? _redis.GetDatabase() : null;

    public async Task UpsertAsync(Guid driverId, double lat, double lng, double? heading = null, CancellationToken ct = default)
    {
        var db = Db();
        if (db is null) { WarnUnavailable(); return; }

        var member = Member(driverId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var batch = db.CreateBatch();
        var add = batch.GeoAddAsync(GeoKey, lng, lat, member);
        var seen = batch.SortedSetAddAsync(SeenKey, member, now);
        // Leave any prior heading in place if this fix didn't include one,
        // rather than clearing it — a slightly stale heading beats none.
        var headingTask = heading.HasValue
            ? batch.HashSetAsync(HeadingKey, member, heading.Value)
            : Task.CompletedTask;
        batch.Execute();
        await Task.WhenAll(add, seen, headingTask);
    }

    public async Task RemoveAsync(Guid driverId, CancellationToken ct = default)
    {
        var db = Db();
        if (db is null) { WarnUnavailable(); return; }

        var member = Member(driverId);
        var batch = db.CreateBatch();
        var geo = batch.GeoRemoveAsync(GeoKey, member);
        var seen = batch.SortedSetRemoveAsync(SeenKey, member);
        var heading = batch.HashDeleteAsync(HeadingKey, member);
        batch.Execute();
        await Task.WhenAll(geo, seen, heading);
    }

    public async Task<IReadOnlyList<NearbyDriver>> QueryNearbyAsync(
        double lat, double lng, double radiusMeters, int limit, CancellationToken ct = default)
    {
        var db = Db();
        if (db is null) { WarnUnavailable(); return Array.Empty<NearbyDriver>(); }

        var results = await db.GeoRadiusAsync(
            GeoKey, lng, lat, radiusMeters, GeoUnit.Meters,
            count: limit, order: Order.Ascending,
            options: GeoRadiusOptions.WithCoordinates | GeoRadiusOptions.WithDistance);

        if (results.Length == 0) return Array.Empty<NearbyDriver>();

        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - FreshnessSeconds;
        var list = new List<NearbyDriver>(results.Length);
        foreach (var r in results)
        {
            if (r.Position is not { } pos) continue;

            var seen = await db.SortedSetScoreAsync(SeenKey, r.Member);
            if (seen is null || seen < cutoff) continue; // stale — driver stopped reporting

            if (!Guid.TryParseExact((string?)r.Member ?? string.Empty, "N", out var id)) continue;

            var headingValue = await db.HashGetAsync(HeadingKey, r.Member);
            double? heading = headingValue.IsNullOrEmpty ? null : (double)headingValue;

            list.Add(new NearbyDriver(id, pos.Latitude, pos.Longitude, r.Distance ?? 0, heading));
        }
        return list;
    }

    public async Task<int> PruneStaleAsync(CancellationToken ct = default)
    {
        var db = Db();
        if (db is null) return 0;

        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - FreshnessSeconds;
        var stale = await db.SortedSetRangeByScoreAsync(SeenKey, double.NegativeInfinity, cutoff);
        if (stale.Length == 0) return 0;

        // GEOADD is just a ZADD under the hood (geohash score), so a plain ZREM
        // removes from the geo set too — no separate "GEOREMOVE" command exists.
        var batch = db.CreateBatch();
        var geo = batch.SortedSetRemoveAsync(GeoKey, stale);
        var seen = batch.SortedSetRemoveAsync(SeenKey, stale);
        var heading = batch.HashDeleteAsync(HeadingKey, stale);
        batch.Execute();
        await Task.WhenAll(geo, seen, heading);
        return stale.Length;
    }

    private static RedisValue Member(Guid driverId) => driverId.ToString("N");

    private void WarnUnavailable() =>
        _log.LogWarning("Redis unavailable — driver-location op skipped. Provision Redis for live matching.");
}
