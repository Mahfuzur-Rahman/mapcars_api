namespace Mapcars.Application.Admins.Dtos;

/// <summary>
/// The full menu catalog annotated for one admin, so the SuperAdmin UI can render
/// a checkbox tree: <see cref="MenuAccessItem.Allowed"/> is the effective state,
/// <see cref="MenuAccessItem.RoleDefault"/> shows what the admin's role grants by default.
/// </summary>
public class AdminMenuAccessResponse
{
    public Guid AdminId { get; set; }
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Role { get; set; } = string.Empty;

    /// <summary>Whole menu catalog as a tree (allowed + not-allowed), for editing.</summary>
    public List<MenuAccessItem> Menus { get; set; } = [];
}

public class MenuAccessItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Effective access for this admin (role default ± per-admin override).</summary>
    public bool Allowed { get; set; }

    /// <summary>Whether this menu is granted by the admin's role out of the box.</summary>
    public bool RoleDefault { get; set; }

    public List<MenuAccessItem> Children { get; set; } = [];
}
