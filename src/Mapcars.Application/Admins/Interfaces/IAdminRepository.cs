using Mapcars.Domain.Entities;

namespace Mapcars.Application.Admins.Interfaces;

public interface IAdminRepository
{
    Task<Admin?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Admin?> GetByIdWithRoleAsync(Guid id, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<bool> AnyAdminsExistAsync(CancellationToken ct = default);
    Task AddAsync(Admin admin, CancellationToken ct = default);
    Task<List<Menu>> GetMenusForAdminAsync(Guid adminId, int roleId, CancellationToken ct = default);
    Task<List<Admin>> GetAllAsync(CancellationToken ct = default);

    // ── Menu management (SuperAdmin) ────────────────────────────────────────────
    Task<List<Menu>> GetAllMenusAsync(CancellationToken ct = default);
    Task<List<int>> GetRoleMenuIdsAsync(int roleId, CancellationToken ct = default);
    Task<List<AdminMenuPermission>> GetAdminOverridesAsync(Guid adminId, CancellationToken ct = default);

    /// <summary>Delete all per-admin overrides for the admin, then add the given deltas.</summary>
    Task ReplaceAdminOverridesAsync(Guid adminId, IEnumerable<AdminMenuPermission> overrides, CancellationToken ct = default);
}
