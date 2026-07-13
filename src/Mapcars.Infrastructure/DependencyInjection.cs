using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Infrastructure.Options;
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

        // Security
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // Email — driven by Email:Provider ("Smtp" | "Resend"); falls back to console stub
        switch (configuration["Email:Provider"]?.Trim().ToLowerInvariant())
        {
            case "smtp":
                services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));
                services.AddScoped<IEmailService, SmtpEmailService>();
                break;
            case "resend":
                services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.Section));
                services.AddScoped<IEmailService, ResendEmailService>();
                services.AddScoped<IInboundEmailService, ResendInboundEmailService>();
                break;
            default:
                services.AddScoped<IEmailService, ConsoleEmailService>();
                break;
        }

        // SMS — driven by Sms:Provider ("Twilio" | "Telnyx"); falls back to console stub
        switch (configuration["Sms:Provider"]?.Trim().ToLowerInvariant())
        {
            case "twilio":
                services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.Section));
                services.AddScoped<ISmsService, TwilioSmsService>();
                break;
            case "telnyx":
                services.Configure<TelnyxOptions>(configuration.GetSection(TelnyxOptions.Section));
                services.AddScoped<ISmsService, TelnyxSmsService>();
                break;
            default:
                services.AddScoped<ISmsService, ConsoleSmsService>();
                break;
        }

        return services;
    }
}
