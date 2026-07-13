using Mapcars.Application.Admins.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class AdminRepository(AppDbContext db) : IAdminRepository
{
    public Task<Admin?> GetByEmailAsync(string email, CancellationToken ct = default)
        => db.Admins
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.Email == email.ToLowerInvariant().Trim(), ct);

    public Task<Admin?> GetByIdWithRoleAsync(Guid id, CancellationToken ct = default)
        => db.Admins
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => db.Admins.AnyAsync(a => a.Email == email.ToLowerInvariant().Trim(), ct);

    public Task<bool> AnyAdminsExistAsync(CancellationToken ct = default)
        => db.Admins.AnyAsync(ct);

    public async Task AddAsync(Admin admin, CancellationToken ct = default)
        => await db.Admins.AddAsync(admin, ct);

    public async Task<List<Menu>> GetMenusForAdminAsync(Guid adminId, int roleId, CancellationToken ct = default)
    {
        var roleMenuIds = await db.RoleMenus
            .Where(rm => rm.RoleId == roleId)
            .Select(rm => rm.MenuId)
            .ToListAsync(ct);

        var overrides = await db.AdminMenuPermissions
            .Where(amp => amp.AdminId == adminId)
            .ToListAsync(ct);

        var revoked = overrides.Where(o => !o.IsAllowed).Select(o => o.MenuId).ToHashSet();
        var granted = overrides.Where(o => o.IsAllowed).Select(o => o.MenuId).ToHashSet();

        var finalIds = roleMenuIds
            .Where(id => !revoked.Contains(id))
            .Concat(granted)
            .Distinct()
            .ToList();

        return await db.Menus
            .Where(m => finalIds.Contains(m.Id) && m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);
    }

    public Task<List<Admin>> GetAllAsync(CancellationToken ct = default)
        => db.Admins.Include(a => a.Role).OrderBy(a => a.CreatedAtUtc).ToListAsync(ct);

    // ── Menu management (SuperAdmin) ────────────────────────────────────────────

    public Task<List<Menu>> GetAllMenusAsync(CancellationToken ct = default)
        => db.Menus.Where(m => m.IsActive).OrderBy(m => m.SortOrder).ToListAsync(ct);

    public Task<List<int>> GetRoleMenuIdsAsync(int roleId, CancellationToken ct = default)
        => db.RoleMenus.Where(rm => rm.RoleId == roleId).Select(rm => rm.MenuId).ToListAsync(ct);

    public Task<List<AdminMenuPermission>> GetAdminOverridesAsync(Guid adminId, CancellationToken ct = default)
        => db.AdminMenuPermissions.Where(amp => amp.AdminId == adminId).ToListAsync(ct);

    public async Task ReplaceAdminOverridesAsync(
        Guid adminId, IEnumerable<AdminMenuPermission> overrides, CancellationToken ct = default)
    {
        var existing = await db.AdminMenuPermissions
            .Where(amp => amp.AdminId == adminId)
            .ToListAsync(ct);
        db.AdminMenuPermissions.RemoveRange(existing);
        await db.AdminMenuPermissions.AddRangeAsync(overrides, ct);
    }
}
