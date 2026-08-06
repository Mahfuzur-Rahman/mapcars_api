namespace Mapcars.Application.Drivers.Dtos;

/// <summary>
/// Full driver profile — richer than the shared AuthResponse (which is used
/// by both riders and drivers across login/signup), so driver-only fields
/// like DOB/address/national ID live here instead.
/// </summary>
public class DriverProfileResponse
{
    public Guid DriverId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? NationalIdNumber { get; set; }
    public string? DrivingLicenceNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool MarketingConsent { get; set; }
    public bool HasProfilePicture { get; set; }
    public bool IsProfileComplete { get; set; }

    /// <summary>Admin approval status — a "PendingApproval"/"Suspended"/"Rejected" driver cannot go online.</summary>
    public string Status { get; set; } = "PendingApproval";

    public bool IsOnline { get; set; }
    public DateTime? LastOnlineAtUtc { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CancellationCount { get; set; }
    public int NoShowCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
