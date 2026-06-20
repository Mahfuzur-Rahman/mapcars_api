namespace Mapcars.Domain.Entities;

public class Menu
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public Menu? Parent { get; set; }
    public ICollection<Menu> Children { get; set; } = [];
    public ICollection<RoleMenu> RoleMenus { get; set; } = [];
    public ICollection<AdminMenuPermission> AdminPermissions { get; set; } = [];
}
