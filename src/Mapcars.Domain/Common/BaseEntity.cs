namespace Mapcars.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Provides identity and audit timestamps.
/// Timestamps are stamped automatically by the DbContext on save.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
