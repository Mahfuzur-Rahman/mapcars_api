using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.DriverReview.Interfaces;

/// <summary>Admin-facing driver verification / document review use-cases.</summary>
public interface IDriverReviewService
{
    Task<IReadOnlyList<DriverReviewListItem>> ListDriversAsync(DriverStatus? status, CancellationToken ct = default);
    Task<DriverReviewDetail> GetDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Streams a document's bytes for an admin to view. Null if the doc or its blob is missing.</summary>
    Task<FileContent?> GetDocumentContentAsync(Guid documentId, CancellationToken ct = default);

    Task<DocumentResponse> ReviewDocumentAsync(Guid documentId, DocumentReviewStatus status, CancellationToken ct = default);
    Task<DriverReviewDetail> SetDriverStatusAsync(Guid driverId, DriverStatus status, CancellationToken ct = default);
}
