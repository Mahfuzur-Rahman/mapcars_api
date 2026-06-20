namespace Mapcars.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Admin> Admins { get; set; } = [];
    public ICollection<RoleMenu> RoleMenus { get; set; } = [];
}
