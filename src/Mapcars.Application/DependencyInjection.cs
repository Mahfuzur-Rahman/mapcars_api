using System.Reflection;
using FluentValidation;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Services;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Documents.Services;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Drivers.Services;
using Mapcars.Application.Payments.Interfaces;
using Mapcars.Application.Payments.Services;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing.Services;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.Riders.Services;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Services;
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
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminManagementService, AdminManagementService>();
        services.AddScoped<IRiderAuthService, RiderAuthService>();
        services.AddScoped<IDriverAuthService, DriverAuthService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IPayoutService, PayoutService>();

        return services;
    }
}
