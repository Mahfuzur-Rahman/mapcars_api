using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Mapping;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Admins.Services;

/// <summary>
/// SuperAdmin-only. Reads the menu catalog and controls each admin's access by
/// diffing the desired menu set against the admin's role defaults, then storing
/// only the deltas in <c>admin_menu_permissions</c> (grant = extra, revoke = removed).
/// This mirrors <see cref="IAdminRepository.GetMenusForAdminAsync"/>.
/// </summary>
public class AdminManagementService(IAdminRepository adminRepo, IUnitOfWork uow) : IAdminManagementService
{
    private const int SuperAdminRoleId = 1;

    public async Task<List<AdminListItemResponse>> GetAllAdminsAsync(CancellationToken ct = default)
    {
        var admins = await adminRepo.GetAllAsync(ct);
        var result = new List<AdminListItemResponse>(admins.Count);
        foreach (var admin in admins)
        {
            var menus = await adminRepo.GetMenusForAdminAsync(admin.Id, admin.RoleId, ct);
            result.Add(admin.ToListItem(menus.Count));
        }
        return result;
    }

    public async Task<List<MenuResponse>> GetMenuCatalogAsync(CancellationToken ct = default)
    {
        var menus = await adminRepo.GetAllMenusAsync(ct);
        return menus.ToMenuTree();
    }

    public async Task<AdminMenuAccessResponse> GetAdminMenuAccessAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await adminRepo.GetByIdWithRoleAsync(adminId, ct)
            ?? throw new NotFoundException("Admin", adminId);

        return await BuildAccessResponseAsync(admin, ct);
    }

    public async Task<AdminMenuAccessResponse> SetAdminMenuAccessAsync(
        Guid adminId, List<int> menuIds, CancellationToken ct = default)
    {
        var admin = await adminRepo.GetByIdWithRoleAsync(adminId, ct)
            ?? throw new NotFoundException("Admin", adminId);

        if (admin.RoleId == SuperAdminRoleId)
            throw new DomainException("A SuperAdmin always has full access and cannot be restricted.");

        var allMenus = await adminRepo.GetAllMenusAsync(ct);
        var validIds = allMenus.Select(m => m.Id).ToHashSet();

        // Desired set = requested ids (that exist) plus every ancestor, so a child
        // is never granted without a path to it in the tree.
        var byId = allMenus.ToDictionary(m => m.Id);
        var desired = new HashSet<int>();
        foreach (var id in menuIds.Where(validIds.Contains))
        {
            var cursor = byId[id];
            while (true)
            {
                desired.Add(cursor.Id);
                if (cursor.ParentId is int pid && byId.TryGetValue(pid, out var parent))
                    cursor = parent;
                else
                    break;
            }
        }

        var roleDefaults = (await adminRepo.GetRoleMenuIdsAsync(admin.RoleId, ct)).ToHashSet();

        // Deltas vs role defaults: grants (desired but not default), revokes (default but not desired).
        var overrides = new List<AdminMenuPermission>();
        foreach (var id in desired.Where(id => !roleDefaults.Contains(id)))
            overrides.Add(new AdminMenuPermission { AdminId = adminId, MenuId = id, IsAllowed = true });
        foreach (var id in roleDefaults.Where(id => !desired.Contains(id)))
            overrides.Add(new AdminMenuPermission { AdminId = adminId, MenuId = id, IsAllowed = false });

        await adminRepo.ReplaceAdminOverridesAsync(adminId, overrides, ct);
        await uow.SaveChangesAsync(ct);

        return await BuildAccessResponseAsync(admin, ct);
    }

    private async Task<AdminMenuAccessResponse> BuildAccessResponseAsync(Admin admin, CancellationToken ct)
    {
        var allMenus = await adminRepo.GetAllMenusAsync(ct);
        var roleDefaults = (await adminRepo.GetRoleMenuIdsAsync(admin.RoleId, ct)).ToHashSet();
        var effective = (await adminRepo.GetMenusForAdminAsync(admin.Id, admin.RoleId, ct))
            .Select(m => m.Id).ToHashSet();

        return new AdminMenuAccessResponse
        {
            AdminId = admin.Id,
            Email = admin.Email,
            RoleId = admin.RoleId,
            Role = admin.Role?.Name ?? string.Empty,
            Menus = allMenus.ToAccessTree(effective, roleDefaults),
        };
    }
}
