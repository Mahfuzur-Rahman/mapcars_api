namespace Mapcars.Application.Drivers.Dtos;

/// <summary>
/// Full driver profile — richer than the shared AuthResponse (which is used
/// by both customers and drivers across login/signup), so driver-only fields
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

    /// <summary>
    /// Which fares this driver can be offered, already resolved: the global
    /// payment setting narrowed by any per-driver override. The raw override is
    /// deliberately NOT sent — it is a tri-state whose meaning depends on the
    /// global setting, and the app has no business reconstructing that rule.
    /// </summary>
    public bool AcceptsCash { get; set; } = true;
    public bool AcceptsCard { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
