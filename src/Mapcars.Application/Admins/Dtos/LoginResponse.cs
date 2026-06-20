namespace Mapcars.Application.Admins.Dtos;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
    public AdminResponse Admin { get; set; } = null!;
    public List<MenuResponse> Menus { get; set; } = [];
}
