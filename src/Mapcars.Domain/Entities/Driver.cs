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

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public bool IsProfileComplete => !string.IsNullOrEmpty(FullName);
}
