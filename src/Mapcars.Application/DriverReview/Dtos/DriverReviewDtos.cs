using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.Vehicles.Dtos;

namespace Mapcars.Application.DriverReview.Dtos;

/// <summary>One row in the admin driver-review queue.</summary>
public record DriverReviewListItem(
    Guid DriverId,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    string Status,
    int DocumentCount,
    int PendingDocumentCount,
    int ExpiredDocumentCount,
    DateTime CreatedAtUtc);

/// <summary>Full picture of one driver for the admin review screen.</summary>
public record DriverReviewDetail(
    Guid DriverId,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Address,
    string? NationalIdNumber,
    string? PhvLicenceNumber,
    string? DrivingLicenceNumber,
    string? PassportNumber,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    bool MarketingConsent,
    decimal? AverageRating,
    int RatingCount,
    int CancellationCount,
    int NoShowCount,
    bool IsOnline,
    string Status,
    bool HasProfilePicture,
    VehicleResponse? Vehicle,
    IReadOnlyList<DocumentResponse> Documents);

/// <summary>Approve or reject a single uploaded document. Status: "Approved" | "Rejected".</summary>
public record ReviewDocumentRequest(string Status);

/// <summary>Set a driver's overall status. Status: one of the DriverStatus names.</summary>
public record SetDriverStatusRequest(string Status);

/// <summary>A file streamed back to an admin for viewing.</summary>
public record FileContent(Stream Content, string ContentType, string FileName);
