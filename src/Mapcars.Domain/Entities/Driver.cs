using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

public class Driver : BaseEntity
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string? GoogleSub { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }

    /// <summary>UK Private Hire Vehicle driver licence number (set during document upload).</summary>
    public string? PhvLicenceNumber { get; set; }

    public DriverStatus Status { get; set; } = DriverStatus.PendingApproval;

    // ── Profile ───────────────────────────────────────────────────────────────
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? NationalIdNumber { get; set; }
    public string? ProfilePictureKey { get; set; }
    public string? ProfilePictureContentType { get; set; }

    /// <summary>DVLA driving licence number — distinct from the PHV licence number above.</summary>
    public string? DrivingLicenceNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool MarketingConsent { get; set; }

    public int CancellationCount { get; set; }
    public int NoShowCount { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastOnlineAtUtc { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public bool IsProfileComplete => !string.IsNullOrEmpty(FullName);
}
