using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

public class Rider : BaseEntity
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string? GoogleSub { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public bool IsProfileComplete => !string.IsNullOrEmpty(FullName);
}
