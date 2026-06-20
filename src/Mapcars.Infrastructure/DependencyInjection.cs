using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Infrastructure.Persistence;
using Mapcars.Infrastructure.Persistence.Repositories;
using Mapcars.Infrastructure.Security;
using Mapcars.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mapcars.Infrastructure;

/// <summary>
/// Registers the Infrastructure (data) layer: DbContext, unit of work, and
/// repositories. Called from the API's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // The unit of work is the same scoped instance as the DbContext.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IRiderRepository, RiderRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();

        // Security & messaging
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ISmsService, ConsoleSmsService>();
        services.AddScoped<IEmailService, ConsoleEmailService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        return services;
    }
}
