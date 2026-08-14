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
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<SavedPlace> SavedPlaces => Set<SavedPlace>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<TripMessage> TripMessages => Set<TripMessage>();
    public DbSet<DriverPayoutAccount> DriverPayoutAccounts => Set<DriverPayoutAccount>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Poster> Posters => Set<Poster>();

    // Central error log — every surface appends here (database/021_error_logs.sql).
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    // Every email send, from any code path (database/022_email_log.sql).
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

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

    // Long-lived refresh tokens — what keeps a signed-in user signed in
    // (database/025_refresh_tokens.sql).
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
