namespace Mapcars.Application.Admins.Dtos;

/// <summary>One row in the SuperAdmin's "Admin Users" list.</summary>
public class AdminListItemResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int MenuCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
