namespace Mapcars.Application.Settings.Interfaces;

/// <summary>
/// Key → document settings, with the same three-tier read path as the fare chart
/// store (process memory → Redis → Postgres) and the same best-effort treatment
/// of Redis.
///
/// <para>
/// <typeparamref name="T"/> must be default-constructible, and its defaults are
/// the seed: asking for a key that has never been published writes
/// <c>new T()</c> as version 1 rather than returning null. That is what lets
/// callers treat settings as always-present.
/// </para>
/// </summary>
public interface ISettingsStore
{
    Task<T> GetAsync<T>(string key, CancellationToken ct = default) where T : class, new();

    /// <summary>Publishes a new version and returns it. Invalidates every instance.</summary>
    Task<T> SetAsync<T>(string key, T value, Guid? updatedByAdminId = null, CancellationToken ct = default)
        where T : class, new();
}
