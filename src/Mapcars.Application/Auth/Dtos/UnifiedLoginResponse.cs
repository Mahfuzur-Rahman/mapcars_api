using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Auth.Dtos;

/// <summary>
/// Single response shape for the role-detecting login endpoint. Only the
/// fields for the matched <see cref="UserType"/> are populated — callers
/// branch on it the same way the per-role endpoints' <c>userType</c> works.
/// </summary>
public class UnifiedLoginResponse
{
    /// <summary>
    /// True when the email+password matched more than one account (e.g. the
    /// same person has both a rider and a driver account under this email).
    /// Every other field is unset — the caller must ask the user which
    /// account they mean, then resend the login with <c>LoginAs</c> set to
    /// one of <see cref="AvailableUserTypes"/>.
    /// </summary>
    public bool RequiresChoice { get; set; }
    public List<string>? AvailableUserTypes { get; set; }

    public string Token { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
    public string UserType { get; set; } = string.Empty; // "admin" | "rider" | "driver"

    // Rider / driver
    public Guid? UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsProfileComplete { get; set; }
    public bool? IsEmailVerified { get; set; }
    public bool? IsPhoneVerified { get; set; }

    // Admin
    public AdminResponse? Admin { get; set; }
    public List<MenuResponse>? Menus { get; set; }
}
