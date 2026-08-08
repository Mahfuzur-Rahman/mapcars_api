using System.Reflection;
using FluentValidation;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Services;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Auth.Services;
using Mapcars.Application.Dispatch.Interfaces;
using Mapcars.Application.Dispatch.Services;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Documents.Services;
using Mapcars.Application.DriverReview.Interfaces;
using Mapcars.Application.DriverReview.Services;
using Mapcars.Application.ErrorLogs.Interfaces;
using Mapcars.Application.ErrorLogs.Services;
using Mapcars.Application.Emails.Interfaces;
using Mapcars.Application.Emails.Services;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Drivers.Services;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Geo.Services;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Notifications.Services;
using Mapcars.Application.Payments.Interfaces;
using Mapcars.Application.Payments.Services;
using Mapcars.Application.Posters.Interfaces;
using Mapcars.Application.Posters.Services;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing.Services;
using Mapcars.Application.Realtime;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Ratings.Interfaces;
using Mapcars.Application.Ratings.Services;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.Riders.Services;
using Mapcars.Application.SavedPlaces.Interfaces;
using Mapcars.Application.SavedPlaces.Services;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Services;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Application.Vehicles.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mapcars.Application;

/// <summary>
/// Registers the Application (business logic) layer with the DI container.
/// Called from the API's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // All FluentValidation validators in this assembly.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Application services (one line per feature service).
        services.AddScoped<IRiderService, RiderService>();
        services.AddScoped<IUnifiedAuthService, UnifiedAuthService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminManagementService, AdminManagementService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IRiderAuthService, RiderAuthService>();
        services.AddScoped<IDriverAuthService, DriverAuthService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IDriverLocationService, DriverLocationService>();
        services.AddScoped<IDispatchService, DispatchService>();

        // Realtime: no-op by default so Application resolves standalone. The API
        // registers a SignalR-backed ITripNotifier after this, which wins.
        services.AddSingleton<ITripNotifier, NullTripNotifier>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<ISavedPlaceService, SavedPlaceService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IDriverReviewService, DriverReviewService>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IPushService, PushService>();
        services.AddScoped<IPosterService, PosterService>();
        services.AddScoped<IErrorLogService, ErrorLogService>();
        services.AddScoped<IEmailAdminService, EmailAdminService>();

        return services;
    }
}
