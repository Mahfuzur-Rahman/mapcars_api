using Mapcars.Application.Admins.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Admins.Mapping;

public static class AdminMappings
{
    public static AdminResponse ToResponse(this Admin admin) => new()
    {
        Id = admin.Id,
        Email = admin.Email,
        FullName = admin.FullName,
        Role = admin.Role?.Name ?? string.Empty,
        IsActive = admin.IsActive,
        CreatedAtUtc = admin.CreatedAtUtc,
    };

    public static List<MenuResponse> ToMenuTree(this List<Menu> menus)
    {
        var lookup = menus
            .OrderBy(m => m.SortOrder)
            .ToDictionary(m => m.Id, m => new MenuResponse
            {
                Id = m.Id,
                Name = m.Name,
                Path = m.Path,
                Icon = m.Icon,
                ParentId = m.ParentId,
                SortOrder = m.SortOrder,
            });

        var roots = new List<MenuResponse>();
        foreach (var item in lookup.Values)
        {
            if (item.ParentId.HasValue && lookup.TryGetValue(item.ParentId.Value, out var parent))
                parent.Children.Add(item);
            else
                roots.Add(item);
        }
        return roots;
    }
}
