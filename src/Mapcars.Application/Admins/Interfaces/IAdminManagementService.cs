using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Admins.Interfaces;

/// <summary>
/// SuperAdmin-only management surface: list admins, browse the menu catalog, and
/// control which menus each admin can see.
/// </summary>
public interface IAdminManagementService
{
    Task<List<AdminListItemResponse>> GetAllAdminsAsync(CancellationToken ct = default);

    /// <summary>The full menu catalog as a tree (everything the platform offers).</summary>
    Task<List<MenuResponse>> GetMenuCatalogAsync(CancellationToken ct = default);

    Task<AdminMenuAccessResponse> GetAdminMenuAccessAsync(Guid adminId, CancellationToken ct = default);

    /// <summary>Replace an admin's menu access with the given complete set of menu ids.</summary>
    Task<AdminMenuAccessResponse> SetAdminMenuAccessAsync(Guid adminId, List<int> menuIds, CancellationToken ct = default);
}
