using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Trips.Emails;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Mapcars.Application.Trips.Services;

/// <summary>
/// Emails both parties when a trip completes — whoever has a <b>verified</b>
/// email on their account. Email sign-ups are verified at registration; a phone
/// sign-up adds an email on the profile and verifies it there
/// (<c>me/email/send-code</c> + <c>me/email/verify</c>). Unverified addresses get
/// nothing: a mistyped one would otherwise receive somebody's pickup and
/// drop-off addresses. Each send is independent and best-effort: one bad address
/// must not cost the other party their email, and nothing here is allowed to
/// throw back into the trip lifecycle.
/// </summary>
public class TripCompletionEmailer(
    ITripRepository trips,
    ICustomerRepository customers,
    IDriverRepository drivers,
    IVehicleRepository vehicles,
    IEmailService email,
    ILogger<TripCompletionEmailer> logger) : ITripCompletionEmailer
{
    public async Task SendAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await trips.GetByIdAsync(tripId, ct);
        if (trip is null || trip.Status != TripStatus.Completed) return;

        var customer = await customers.GetByIdAsync(trip.CustomerId, ct);
        var driver = trip.DriverId is Guid driverId ? await drivers.GetByIdAsync(driverId, ct) : null;
        var vehicle = driver is null ? null : await vehicles.GetByDriverAsync(driver.Id, ct);

        if (TripCompletionEmails.AddressFor(customer) is { } customerEmail)
        {
            var (subject, body) = TripCompletionEmails.ForCustomer(trip, customer!, driver, vehicle);
            await TrySendAsync(customerEmail, subject, body, "TripReceipt", tripId, ct);
        }

        if (TripCompletionEmails.AddressFor(driver) is { } driverEmail)
        {
            var (subject, body) = TripCompletionEmails.ForDriver(trip, driver!, customer);
            await TrySendAsync(driverEmail, subject, body, "DriverTripSummary", tripId, ct);
        }
    }

    private async Task TrySendAsync(
        string to, string subject, string body, string category, Guid tripId, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(to, subject, body, new EmailSendOptions(Category: category), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to send {Category} email for trip {TripId}", category, tripId);
        }
    }
}
