namespace Mapcars.Domain.Entities;

public class Admin
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Role Role { get; set; } = null!;
    public Admin? Creator { get; set; }
    public ICollection<AdminMenuPermission> MenuPermissions { get; set; } = [];
}
