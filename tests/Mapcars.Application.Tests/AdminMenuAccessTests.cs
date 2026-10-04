using Mapcars.Application.Admins;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Services;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Tests;

/// <summary>
/// The server-side menu check behind <c>[RequiresAdminMenu]</c>. Until it existed
/// the portal merely HID menus an Admin wasn't allowed, while the API checked only
/// the role — so any Admin could call every admin endpoint directly. These pin
/// that the API now applies the very rule the sidebar is built from.
/// </summary>
public class AdminMenuAccessTests
{
    private const int AdminRoleId = 2;

    // A slice of the real catalog. Paths as seeded; ids too for the 001 rows (later
    // rows take sequence ids, so 17 and 30 here are stand-ins).
    private static readonly Menu Dashboard = NewMenu(1, "/admin");
    private static readonly Menu Drivers = NewMenu(5, path: null);  // a parent: no page of its own
    private static readonly Menu DriverList = NewMenu(6, AdminMenuPaths.DriverList, parentId: 5);
    private static readonly Menu DriverDetail = NewMenu(7, AdminMenuPaths.DriverDetail, parentId: 5);
    private static readonly Menu Documents = NewMenu(8, AdminMenuPaths.DriverDocuments, parentId: 5);
    private static readonly Menu AdminUsers = NewMenu(15, AdminMenuPaths.AdminUsers);
    private static readonly Menu FareSettings = NewMenu(17, AdminMenuPaths.FareSettings);
    private static readonly Menu Statements = NewMenu(30, AdminMenuPaths.Statements);

    private static readonly List<Menu> Catalog =
        [Dashboard, Drivers, DriverList, DriverDetail, Documents, AdminUsers, FareSettings, Statements];

    // 001 gives the Admin role everything but Admin Users; Fare Settings (016) is SuperAdmin-only.
    private static readonly int[] AdminRoleDefaults = [1, 5, 6, 7, 8, 30];

    private static Menu NewMenu(int id, string? path, int? parentId = null)
        => new() { Id = id, Name = path ?? "parent", Path = path, ParentId = parentId, IsActive = true };

    private static AdminMenuPermission Grant(int menuId) => new() { MenuId = menuId, IsAllowed = true };
    private static AdminMenuPermission Revoke(int menuId) => new() { MenuId = menuId, IsAllowed = false };

    private static AdminAccessProfile AnAdmin(params AdminMenuPermission[] overrides)
        => new(Guid.NewGuid(), AdminRoleId, IsActive: true, AdminRoleDefaults, overrides, Catalog);

    private static AdminAccessProfile ASuperAdmin()
        => new(Guid.NewGuid(), AdminMenuAccess.SuperAdminRoleId, IsActive: true, RoleMenuIds: [], Overrides: [], Menus: []);

    private static bool CanUse(AdminAccessProfile? admin, params string[] anyOf)
        => AdminMenuAccess.CanUseAny(admin, anyOf);

    // ── The decision ────────────────────────────────────────────────────────────

    [Fact]
    public void A_SuperAdmin_passes_every_menu_check()
    {
        // Full access by definition — even with no role_menus rows at all, and
        // even for a path no menu carries. They cannot be restricted.
        Assert.True(CanUse(ASuperAdmin(), AdminMenuPaths.DriverList));
        Assert.True(CanUse(ASuperAdmin(), "/admin/a-page-added-next-year"));
    }

    [Fact]
    public void An_admin_passes_for_a_menu_their_role_grants()
        => Assert.True(CanUse(AnAdmin(), AdminMenuPaths.DriverList));

    [Fact]
    public void An_admin_is_denied_a_menu_their_role_does_not_grant()
    {
        Assert.False(CanUse(AnAdmin(), AdminMenuPaths.AdminUsers));
        Assert.False(CanUse(AnAdmin(), AdminMenuPaths.FareSettings));
    }

    [Fact]
    public void A_revoke_override_removes_a_role_default()
    {
        var admin = AnAdmin(Revoke(DriverList.Id));

        Assert.False(CanUse(admin, AdminMenuPaths.DriverList));
        Assert.True(CanUse(admin, AdminMenuPaths.DriverDocuments)); // the rest of the role is untouched
    }

    [Fact]
    public void A_grant_override_adds_a_menu_the_role_lacks()
        => Assert.True(CanUse(AnAdmin(Grant(FareSettings.Id)), AdminMenuPaths.FareSettings));

    [Fact]
    public void A_disabled_admin_is_denied_even_a_menu_they_hold()
    {
        var admin = AnAdmin() with { IsActive = false };

        Assert.False(CanUse(admin, AdminMenuPaths.DriverList));
    }

    [Fact]
    public void A_disabled_SuperAdmin_is_denied_too()
    {
        // "Disabled" outranks "full access": the account is switched off.
        var superAdmin = ASuperAdmin() with { IsActive = false };

        Assert.False(CanUse(superAdmin, AdminMenuPaths.DriverList));
    }

    [Fact]
    public void An_id_that_is_not_an_admin_is_denied()
    {
        // A customer or driver token carries a sub, but no admin row matches it.
        Assert.False(CanUse(null, AdminMenuPaths.DriverList));
    }

    [Fact]
    public void A_multi_menu_endpoint_passes_when_the_admin_holds_any_one_of_them()
    {
        // e.g. document review: used by the Documents page AND Driver Detail.
        var detailOnly = AnAdmin(Revoke(Documents.Id));
        var documentsOnly = AnAdmin(Revoke(DriverDetail.Id));
        var neither = AnAdmin(Revoke(Documents.Id), Revoke(DriverDetail.Id));

        Assert.True(CanUse(detailOnly, AdminMenuPaths.DriverDocuments, AdminMenuPaths.DriverDetail));
        Assert.True(CanUse(documentsOnly, AdminMenuPaths.DriverDocuments, AdminMenuPaths.DriverDetail));
        Assert.False(CanUse(neither, AdminMenuPaths.DriverDocuments, AdminMenuPaths.DriverDetail));
    }

    [Fact]
    public void An_admin_is_denied_when_no_menu_is_named()
        => Assert.False(CanUse(AnAdmin()));

    [Fact]
    public void An_inactive_menu_grants_nothing()
    {
        // Menus are seeded inactive ahead of their page (042 → 043). The sidebar
        // hides them, so the API must not honour them either.
        var hidden = NewMenu(30, AdminMenuPaths.Statements);
        hidden.IsActive = false;
        var admin = AnAdmin() with { Menus = [.. Catalog.Where(m => m.Id != 30), hidden] };

        Assert.False(CanUse(admin, AdminMenuPaths.Statements));
    }

    [Fact]
    public void Parent_and_child_menus_are_independent_exactly_as_in_the_sidebar()
    {
        // The effective-access rule has no parent/child inference (the editor adds
        // ancestors at save time; reading never does), so neither does the check.
        var parentRevoked = AnAdmin(Revoke(Drivers.Id));
        Assert.True(CanUse(parentRevoked, AdminMenuPaths.DriverList));

        var listRevoked = AnAdmin(Revoke(DriverList.Id));
        Assert.False(CanUse(listRevoked, AdminMenuPaths.DriverList));

        // ...and no prefix matching: holding Driver Detail is not holding Driver List.
        var detailOnly = new AdminAccessProfile(Guid.NewGuid(), AdminRoleId, true, [7], [], Catalog);
        Assert.False(CanUse(detailOnly, AdminMenuPaths.DriverList));
        Assert.True(CanUse(detailOnly, AdminMenuPaths.DriverDetail));
    }

    // ── The shared rule (what the portal's sidebar is built from) ───────────────

    [Fact]
    public void Effective_menus_are_role_defaults_minus_revokes_plus_grants_active_only()
    {
        var hiddenStatements = NewMenu(30, AdminMenuPaths.Statements);
        hiddenStatements.IsActive = false;
        List<Menu> menus = [.. Catalog.Where(m => m.Id != 30), hiddenStatements];

        var effective = AdminMenuAccess.EffectiveMenus(
            menus, AdminRoleDefaults, [Revoke(DriverList.Id), Grant(FareSettings.Id)]);

        Assert.Equal(new[] { 1, 5, 7, 8, 17 }, effective.Select(m => m.Id).Order());
    }

    [Fact]
    public void An_override_for_a_menu_the_catalog_lacks_grants_nothing()
    {
        var effective = AdminMenuAccess.EffectiveMenus(Catalog, AdminRoleDefaults, [Grant(999)]);

        Assert.DoesNotContain(effective, m => m.Id == 999);
    }

    // ── The per-request service ─────────────────────────────────────────────────

    [Fact]
    public async Task The_authorizer_looks_the_admin_up_once_per_request()
    {
        var admin = AnAdmin();
        var repo = new CountingAccessRepo(admin);
        var authorizer = new AdminMenuAuthorizer(repo);

        Assert.True(await authorizer.CanUseAnyAsync(admin.AdminId, [AdminMenuPaths.DriverList]));
        Assert.False(await authorizer.CanUseAnyAsync(admin.AdminId, [AdminMenuPaths.AdminUsers]));

        Assert.Equal(1, repo.Lookups);
    }

    [Fact]
    public async Task The_authorizer_denies_an_id_with_no_admin_row()
    {
        var authorizer = new AdminMenuAuthorizer(new CountingAccessRepo(profile: null));

        Assert.False(await authorizer.CanUseAnyAsync(Guid.NewGuid(), [AdminMenuPaths.DriverList]));
    }

    private sealed class CountingAccessRepo(AdminAccessProfile? profile) : IAdminMenuAccessRepository
    {
        public int Lookups { get; private set; }

        public Task<AdminAccessProfile?> GetAccessProfileAsync(Guid adminId, CancellationToken ct = default)
        {
            Lookups++;
            return Task.FromResult(profile is not null && profile.AdminId == adminId ? profile : null);
        }
    }
}
