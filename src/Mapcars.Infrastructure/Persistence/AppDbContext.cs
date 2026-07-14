using System.Reflection;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Common;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence;

/// <summary>
/// EF Core database context — the data layer. Also implements IUnitOfWork so the
/// Application layer can commit without depending on EF Core.
/// </summary>
public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DriverPayoutAccount> DriverPayoutAccounts => Set<DriverPayoutAccount>();
    public DbSet<Payout> Payouts => Set<Payout>();

    // Pricing — fare chart version history (database-first via database/008_*.sql).
    // Redis is the hot cache; this table is the durable source of truth.
    public DbSet<FareChartRecord> FareCharts => Set<FareChartRecord>();

    // Admin auth (database-first — tables created via database/001_admin_auth.sql)
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
    public DbSet<AdminMenuPermission> AdminMenuPermissions => Set<AdminMenuPermission>();

    // Rider/Driver auth (database-first — tables altered via database/002_rider_driver_auth.sql)
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Stamp audit timestamps centrally.
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
