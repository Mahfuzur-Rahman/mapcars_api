using Amazon.S3;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.ErrorLogs.Interfaces;
using Mapcars.Application.Emails.Interfaces;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Payments.Interfaces;
using Mapcars.Application.Posters.Interfaces;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Ratings.Interfaces;
using Mapcars.Application.Messages.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.SavedPlaces.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Infrastructure.Geo;
using Mapcars.Infrastructure.Notifications;
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
using Microsoft.Extensions.Hosting;
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
        services.AddScoped<IAdminReportingRepository, AdminReportingRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleTierAppealRepository, VehicleTierAppealRepository>();
        services.AddScoped<ISavedPlaceRepository, SavedPlaceRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IDriverPayoutAccountRepository, DriverPayoutAccountRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPosterRepository, PosterRepository>();
        services.AddScoped<IErrorLogRepository, ErrorLogRepository>();
        services.AddScoped<IEmailLogRepository, EmailLogRepository>();

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

        // Live driver locations — Redis GEO (matching hot path). Same optional-Redis
        // pattern as the fare store: no-ops / empty results when Redis is unavailable.
        services.AddSingleton<IDriverLocationStore>(sp => new RedisDriverLocationStore(
            sp.GetService<IConnectionMultiplexer>(),
            sp.GetRequiredService<ILogger<RedisDriverLocationStore>>()));
        services.AddHostedService<GeoStalenessSweepService>();

        // Security
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // Storage — driven by Storage:Provider ("LocalDisk" | "R2"); defaults to
        // local disk. R2 is a private, S3-compatible bucket (reads are always
        // proxied through authenticated API endpoints — never a public URL).
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        switch (configuration["Storage:Provider"]?.Trim().ToLowerInvariant())
        {
            case "r2":
                var r2Section = configuration.GetSection(R2Options.Section);
                services.Configure<R2Options>(r2Section);
                var r2 = r2Section.Get<R2Options>()
                    ?? throw new InvalidOperationException("Storage:Provider is 'R2' but the Storage:R2 section is missing.");
                services.AddSingleton<IAmazonS3>(_ =>
                {
                    var config = new AmazonS3Config
                    {
                        ServiceURL = r2.ResolveServiceUrl(),
                        // R2 requires path-style addressing; region is ignored but
                        // the SDK needs one to compute the SigV4 signature.
                        ForcePathStyle = true,
                        AuthenticationRegion = "auto",
                    };
                    return new AmazonS3Client(r2.AccessKeyId, r2.SecretAccessKey, config);
                });
                services.AddScoped<IFileStorageService, R2FileStorageService>();
                break;
            default:
                services.AddScoped<IFileStorageService, LocalDiskFileStorageService>();
                break;
        }

        // Push notifications — FCM when a Firebase service account is configured
        // (Firebase:ServiceAccountPath), else a console stub so registration/notify
        // still works in dev without credentials. Same provider-switch shape as
        // Email/SMS below.
        var firebaseCredPath = configuration["Firebase:ServiceAccountPath"];
        if (!string.IsNullOrWhiteSpace(firebaseCredPath) && System.IO.File.Exists(firebaseCredPath))
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(firebaseCredPath),
                });
            }
            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, ConsolePushSender>();
        }

        // Stripe Connect — the SDK is configured via a static ApiKey (no client instance to inject).
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.Section));
        StripeConfiguration.ApiKey = configuration[$"{StripeOptions.Section}:SecretKey"];
        services.AddScoped<IStripeConnectService, StripeConnectService>();

        // Email — driven by Email:Provider ("Smtp" | "Resend"); falls back to console stub.
        // Whichever provider is chosen is wrapped in LoggingEmailService, which is what
        // actually gets registered as IEmailService — every send, from any call site,
        // ends up recorded in email_log (see LoggingEmailService, database/022_email_log.sql).
        switch (configuration["Email:Provider"]?.Trim().ToLowerInvariant())
        {
            case "smtp":
                services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));
                services.AddScoped<SmtpEmailService>();
                services.AddScoped<IEmailService>(sp => new LoggingEmailService(
                    sp.GetRequiredService<SmtpEmailService>(),
                    sp.GetRequiredService<IEmailLogRepository>(),
                    sp.GetRequiredService<ILogger<LoggingEmailService>>()));
                break;
            case "resend":
                services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.Section));
                services.AddScoped<ResendEmailService>();
                services.AddScoped<IEmailService>(sp => new LoggingEmailService(
                    sp.GetRequiredService<ResendEmailService>(),
                    sp.GetRequiredService<IEmailLogRepository>(),
                    sp.GetRequiredService<ILogger<LoggingEmailService>>()));
                services.AddScoped<IInboundEmailService, ResendInboundEmailService>();
                break;
            default:
                services.AddScoped<ConsoleEmailService>();
                services.AddScoped<IEmailService>(sp => new LoggingEmailService(
                    sp.GetRequiredService<ConsoleEmailService>(),
                    sp.GetRequiredService<IEmailLogRepository>(),
                    sp.GetRequiredService<ILogger<LoggingEmailService>>()));
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
