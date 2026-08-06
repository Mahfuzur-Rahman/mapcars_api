using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A rider- or driver-uploaded document (identity/KYC for riders; PHV
/// licence/vehicle docs for drivers). Exactly one of RiderId/DriverId is set —
/// enforced by a DB CHECK constraint, mirroring Trip's nullable DriverId.
/// </summary>
public class Document : BaseEntity
{
    public Guid? RiderId { get; set; }
    public Rider? Rider { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public DocumentType Type { get; set; }
    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }

    public DocumentReviewStatus ReviewStatus { get; set; } = DocumentReviewStatus.Pending;
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>Renewal deadline for licence-like documents (PHV licence, insurance, vehicle registration, DBS).</summary>
    public DateOnly? ExpiresOn { get; set; }
}
