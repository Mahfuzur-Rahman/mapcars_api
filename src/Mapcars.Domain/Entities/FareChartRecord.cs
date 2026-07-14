using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// Durable audit row for a published fare chart. Redis is the hot cache for the
/// current chart (fast reads); this table is the source-of-truth history so the
/// chart survives a Redis flush and every version stays traceable.
///
/// The structured chart is stored as a JSON blob (<see cref="PayloadJson"/>) —
/// pricing config is read/written as a whole document, never queried column-wise,
/// so a JSON column keeps the schema stable as pricing rules evolve.
/// </summary>
public class FareChartRecord : BaseEntity
{
    /// <summary>Monotonic chart version. The row with the highest version is current.</summary>
    public int Version { get; set; }

    /// <summary>Serialized <c>FareChart</c> (Application/Pricing/Models) as JSON.</summary>
    public required string PayloadJson { get; set; }
}
