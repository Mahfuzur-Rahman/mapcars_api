using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A customer- or driver-uploaded document (identity/KYC for customers; PHV
/// licence/vehicle docs for drivers). Exactly one of CustomerId/DriverId is set —
/// enforced by a DB CHECK constraint, mirroring Trip's nullable DriverId.
/// </summary>
public class Document : BaseEntity
{
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

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

    /// <summary>Set when the driver/customer requests this document to be deleted.</summary>
    public bool IsDeletionRequested { get; set; } = false;
    public string? DeletionReason { get; set; }
    public DateTime? DeletionRequestedAtUtc { get; set; }
}
