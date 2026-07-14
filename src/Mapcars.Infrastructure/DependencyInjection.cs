using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Payments.Interfaces;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Infrastructure.Options;
using Mapcars.Infrastructure.Payments;
using Mapcars.Infrastructure.Persistence;
using Mapcars.Infrastructure.Persistence.Repositories;
using Mapcars.Infrastructure.Pricing;
using Mapcars.Infrastructure.Security;
using Mapcars.Infrastructure.Services;
using Mapcars.Infrastructure.Storage;
using Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

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
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDriverPayoutAccountRepository, DriverPayoutAccountRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();

        // Pricing — Redis-cached fare chart, durably backed by Postgres.
        // The multiplexer connects lazily with AbortOnConnectFail=false so the API
        // still boots (and prices from Postgres) when Redis is unreachable.
        var redisConfig = configuration.GetSection("Redis")["Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConfig))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConfig);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            });
        }
        // Singleton store: holds the in-memory cache + Redis subscription. Resolves
        // the multiplexer optionally (null when Redis isn't configured).
        services.AddSingleton<IFareChartStore>(sp => new RedisFareChartStore(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<RedisFareChartStore>>(),
            sp.GetService<IConnectionMultiplexer>()));

        // Security
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // Storage — local disk for now; swap for an S3-backed IFileStorageService later.
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        services.AddScoped<IFileStorageService, LocalDiskFileStorageService>();

        // Stripe Connect — the SDK is configured via a static ApiKey (no client instance to inject).
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.Section));
        StripeConfiguration.ApiKey = configuration[$"{StripeOptions.Section}:SecretKey"];
        services.AddScoped<IStripeConnectService, StripeConnectService>();

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
