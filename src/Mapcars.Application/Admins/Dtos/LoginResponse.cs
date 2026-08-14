namespace Mapcars.Application.Admins.Dtos;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }

    /// <summary>Long-lived credential for <c>POST /api/v1/auth/refresh</c>, so an
    /// admin session survives past the access token's short life.</summary>
    public string RefreshToken { get; set; } = string.Empty;
    public AdminResponse Admin { get; set; } = null!;
    public List<MenuResponse> Menus { get; set; } = [];
}
