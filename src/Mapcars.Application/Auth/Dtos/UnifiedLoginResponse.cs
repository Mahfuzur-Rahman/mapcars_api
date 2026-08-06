using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Auth.Dtos;

/// <summary>
/// Single response shape for the role-detecting login endpoint. Only the
/// fields for the matched <see cref="UserType"/> are populated — callers
/// branch on it the same way the per-role endpoints' <c>userType</c> works.
/// </summary>
public class UnifiedLoginResponse
{
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
