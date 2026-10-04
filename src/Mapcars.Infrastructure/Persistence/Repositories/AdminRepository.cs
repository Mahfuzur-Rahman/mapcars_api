using Mapcars.Application.Admins;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class AdminRepository(AppDbContext db) : IAdminRepository, IAdminMenuAccessRepository
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

        // Load every menu either list names; which of them the admin actually gets
        // is AdminMenuAccess's call — the same rule the API's [RequiresAdminMenu]
        // check applies, so the sidebar and the server can't disagree.
        var candidateIds = roleMenuIds.Concat(overrides.Select(o => o.MenuId)).Distinct().ToList();
        var candidates = await db.Menus
            .Where(m => candidateIds.Contains(m.Id))
            .ToListAsync(ct);

        return AdminMenuAccess.EffectiveMenus(candidates, roleMenuIds, overrides)
            .OrderBy(m => m.SortOrder)
            .ToList();
    }

    public async Task<AdminAccessProfile?> GetAccessProfileAsync(Guid adminId, CancellationToken ct = default)
    {
        // One round trip on purpose: this runs on every menu-gated admin call. Both
        // lists come back joined to the menu they name, so no third query is needed.
        // AsSingleQuery is explicit (not just the default) to say the two sibling
        // collections multiplying each other's rows is accepted: they are a couple
        // of dozen rows at most, which costs less than another trip to the database.
        var row = await db.Admins
            .AsNoTracking()
            .AsSingleQuery()
            .Where(a => a.Id == adminId)
            .Select(a => new
            {
                a.Id,
                a.RoleId,
                a.IsActive,
                RoleMenus = a.Role.RoleMenus.Select(rm => rm.Menu).ToList(),
                Overrides = a.MenuPermissions.Select(p => new { p.MenuId, p.IsAllowed, p.Menu }).ToList(),
            })
            .SingleOrDefaultAsync(ct);

        if (row is null) return null;

        return new AdminAccessProfile(
            row.Id,
            row.RoleId,
            row.IsActive,
            RoleMenuIds: row.RoleMenus.Select(m => m.Id).ToList(),
            Overrides: row.Overrides
                .Select(o => new AdminMenuPermission { AdminId = row.Id, MenuId = o.MenuId, IsAllowed = o.IsAllowed })
                .ToList(),
            Menus: row.RoleMenus.Concat(row.Overrides.Select(o => o.Menu)).DistinctBy(m => m.Id).ToList());
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
