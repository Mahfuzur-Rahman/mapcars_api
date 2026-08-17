using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.DriverReview.Interfaces;

/// <summary>Admin-facing driver verification, vehicle tier management, and document review use-cases.</summary>
public interface IDriverReviewService
{
    Task<IReadOnlyList<DriverReviewListItem>> ListDriversAsync(DriverStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<DriverDocumentListItem>> ListAllDocumentsAsync(DocumentReviewStatus? status = null, CancellationToken ct = default);
    Task<DriverReviewDetail> GetDriverAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Streams a document's bytes for an admin to view. Null if the doc or its blob is missing.</summary>
    Task<FileContent?> GetDocumentContentAsync(Guid documentId, CancellationToken ct = default);

    Task<DocumentResponse> ReviewDocumentAsync(Guid documentId, DocumentReviewStatus status, CancellationToken ct = default);
    Task<DriverReviewDetail> SetDriverStatusAsync(Guid driverId, DriverStatus status, CancellationToken ct = default);

    // Vehicle Tier & Appeals
    Task<VehicleResponse> SetVehicleTierAsync(Guid driverId, string tier, CancellationToken ct = default);
    Task<IReadOnlyList<TierAppealListItem>> ListTierAppealsAsync(TierAppealStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleTierAppealResponse>> GetTierAppealsForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<FileContent?> GetAppealPhotoContentAsync(Guid appealId, int photoIndex, CancellationToken ct = default);
    Task<VehicleTierAppealResponse> ReviewTierAppealAsync(Guid appealId, Guid adminId, TierAppealStatus status, string? adminNotes = null, CancellationToken ct = default);
}
