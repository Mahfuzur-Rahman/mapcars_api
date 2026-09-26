using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// One published version of one settings key. Append-only: the current value for
/// a key is its highest <see cref="Version"/>, and every earlier row stays as the
/// audit trail for "who turned card payments off on the 14th, and what was it
/// before?".
///
/// <para>
/// Mirrors <see cref="FareChartRecord"/> deliberately — same shape, same store
/// pattern — but kept separate so that flipping a payment toggle does not mint a
/// pricing version and muddy the fare-chart history.
/// </para>
/// </summary>
public class AppSettingRecord : BaseEntity
{
    /// <summary>Settings key, e.g. "payments". See <c>SettingKeys</c>.</summary>
    public required string Key { get; set; }

    /// <summary>Monotonic per key. The store computes it inside its write gate.</summary>
    public int Version { get; set; }

    public required string PayloadJson { get; set; }

    /// <summary>Which admin published this version. Null for a seeded default.</summary>
    public Guid? UpdatedByAdminId { get; set; }
}
