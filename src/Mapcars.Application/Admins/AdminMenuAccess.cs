using Mapcars.Domain.Entities;

namespace Mapcars.Application.Admins;

/// <summary>
/// Everything the menu rule needs to know about one admin, read in a single round
/// trip by <see cref="Interfaces.IAdminMenuAccessRepository"/>.
/// </summary>
/// <param name="RoleMenuIds">The role's defaults (<c>role_menus</c>).</param>
/// <param name="Overrides">The admin's own deltas (<c>admin_menu_permissions</c>).</param>
/// <param name="Menus">
/// At least every menu those two lists name. Inactive rows are included on purpose:
/// filtering them out is part of the rule, so it happens in <see cref="AdminMenuAccess"/>.
/// </param>
public sealed record AdminAccessProfile(
    Guid AdminId,
    int RoleId,
    bool IsActive,
    IReadOnlyCollection<int> RoleMenuIds,
    IReadOnlyCollection<AdminMenuPermission> Overrides,
    IReadOnlyCollection<Menu> Menus);

/// <summary>
/// Which admin-portal menus an admin may use. This is the one rule behind both the
/// portal's sidebar (<c>IAdminRepository.GetMenusForAdminAsync</c>, returned by
/// login and <c>/admin/auth/me</c>) and the API's server-side check
/// (<c>[RequiresAdminMenu]</c>). It lives here, free of EF, so the two cannot drift:
/// the sidebar only hides a menu, so if the API ever disagreed, hiding would be
/// all that stood between an Admin and the endpoint.
/// </summary>
public static class AdminMenuAccess
{
    /// <summary>
    /// The SuperAdmin role row (seeded first in 001). Full access, and cannot be
    /// restricted — <c>AdminManagementService</c> refuses to store overrides for it.
    /// </summary>
    public const int SuperAdminRoleId = 1;

    /// <summary>
    /// The admin's effective menus: the role's defaults, minus the admin's revokes,
    /// plus the admin's grants — and only menus that are active (a menu is seeded
    /// inactive until its page ships, e.g. 042 → 043).
    /// <para>
    /// There is deliberately no parent/child inference. The portal's editor adds a
    /// child's ancestors when it saves, but reading never infers either way, and
    /// the sidebar renders an orphaned child at the top level — so a child is held
    /// exactly when it is in this list, whatever happened to its parent.
    /// </para>
    /// </summary>
    public static List<Menu> EffectiveMenus(
        IEnumerable<Menu> menus, IEnumerable<int> roleMenuIds, IEnumerable<AdminMenuPermission> overrides)
    {
        var overrideList = overrides.ToList();
        var revoked = overrideList.Where(o => !o.IsAllowed).Select(o => o.MenuId).ToHashSet();
        var granted = overrideList.Where(o => o.IsAllowed).Select(o => o.MenuId);

        var effectiveIds = roleMenuIds
            .Where(id => !revoked.Contains(id))
            .Concat(granted)
            .ToHashSet();

        return menus.Where(m => m.IsActive && effectiveIds.Contains(m.Id)).ToList();
    }

    /// <summary>
    /// Whether the admin may call an endpoint that serves any of the menus at
    /// <paramref name="menuPaths"/> (<see cref="Menu.Path"/>, matched exactly — no
    /// prefix matching, so Driver List is not Driver Detail).
    /// <para>
    /// No admin row (a customer/driver token, or a deleted admin) and a disabled
    /// account are refused before the role is even looked at: disabled outranks
    /// full access.
    /// </para>
    /// </summary>
    public static bool CanUseAny(AdminAccessProfile? admin, IReadOnlyCollection<string> menuPaths)
    {
        if (admin is null || !admin.IsActive) return false;
        if (admin.RoleId == SuperAdminRoleId) return true;

        return EffectiveMenus(admin.Menus, admin.RoleMenuIds, admin.Overrides)
            .Any(m => m.Path is not null && menuPaths.Contains(m.Path, StringComparer.Ordinal));
    }
}
