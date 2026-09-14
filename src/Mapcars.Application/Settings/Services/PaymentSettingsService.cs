using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Settings.Dtos;
using Mapcars.Application.Settings.Interfaces;
using Mapcars.Application.Settings.Models;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Settings.Services;

/// <summary>
/// Reads and publishes the global payment toggles, and the per-driver overrides
/// that narrow them.
/// </summary>
public class PaymentSettingsService(
    ISettingsStore settings,
    IDriverRepository drivers,
    IUnitOfWork uow) : IPaymentSettingsService
{
    public async Task<PaymentSettingsResponse> GetAsync(CancellationToken ct = default)
        => ToResponse(await settings.GetAsync<PaymentSettings>(SettingKeys.Payments, ct));

    public async Task<AdminPaymentSettingsResponse> GetForAdminAsync(CancellationToken ct = default)
        => ToAdminResponse(await settings.GetAsync<PaymentSettings>(SettingKeys.Payments, ct));

    public async Task<AdminPaymentSettingsResponse> UpdateAsync(
        UpdatePaymentSettingsRequest request, Guid adminId, CancellationToken ct = default)
    {
        // Shape, and the at-least-one-method rule, are already enforced by the
        // validator at the API boundary. What is left is the correction below.
        var next = new PaymentSettings
        {
            CashEnabled = request.CashEnabled,
            CardEnabled = request.CardEnabled,
            DefaultMethod = request.DefaultMethod,

            ChallengeOnNewDevice = request.ChallengeOnNewDevice,
            ChallengeAfterFailedCharge = request.ChallengeAfterFailedCharge,
            ChallengeUnauthenticatedCards = request.ChallengeUnauthenticatedCards,
            ChallengeAboveFarePence = request.ChallengeAboveFarePence,
            ReverifyAfterDormantDays = request.ReverifyAfterDormantDays,

            MaxSavedCardsPerCustomer = request.MaxSavedCardsPerCustomer,
            MaxCardAddAttemptsPerDay = request.MaxCardAddAttemptsPerDay,
            BlockBookingWhenDebtExceedsPence = request.BlockBookingWhenDebtExceedsPence,
        };

        // Correct rather than reject: switching off whichever method happens to be
        // the default is an ordinary admin action, and a 400 would only force them
        // to do it in two steps. Clients are then never handed a default method
        // they are not allowed to offer.
        if (next.DefaultMethod == PaymentMethodNames.Cash && !next.CashEnabled)
            next.DefaultMethod = PaymentMethodNames.Card;
        else if (next.DefaultMethod == PaymentMethodNames.Card && !next.CardEnabled)
            next.DefaultMethod = PaymentMethodNames.Cash;

        var saved = await settings.SetAsync(SettingKeys.Payments, next, adminId, ct);
        return ToAdminResponse(saved);
    }

    public async Task<DriverPaymentOptionsResponse> GetDriverOptionsAsync(
        Guid driverId, CancellationToken ct = default)
    {
        var driver = await drivers.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);
        var current = await settings.GetAsync<PaymentSettings>(SettingKeys.Payments, ct);
        return ToResponse(driver, current);
    }

    public async Task<DriverPaymentOptionsResponse> UpdateDriverOptionsAsync(
        Guid driverId, UpdateDriverPaymentOptionsRequest request, CancellationToken ct = default)
    {
        var driver = await drivers.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        var current = await settings.GetAsync<PaymentSettings>(SettingKeys.Payments, ct);

        driver.AcceptsCashOverride = request.AcceptsCashOverride;
        driver.AcceptsCardOverride = request.AcceptsCardOverride;

        // A driver who can take neither method can never be dispatched anything,
        // and from their side that looks like the platform has simply gone quiet —
        // no error, no explanation, just an empty board. Refuse it here rather
        // than let an admin strand someone by accident.
        if (!DriverPaymentOptions.AcceptsCash(current, driver) &&
            !DriverPaymentOptions.AcceptsCard(current, driver))
        {
            throw new DomainException(
                "That would leave this driver unable to accept any trip. Allow at least one payment " +
                "method for them, or check the global payment settings.");
        }

        await uow.SaveChangesAsync(ct);
        return ToResponse(driver, current);
    }

    /// <summary>The public shape — methods only. See the DTO for why.</summary>
    private static PaymentSettingsResponse ToResponse(PaymentSettings s)
        => new(s.CashEnabled, s.CardEnabled, s.DefaultMethod);

    private static AdminPaymentSettingsResponse ToAdminResponse(PaymentSettings s)
        => new(s.CashEnabled, s.CardEnabled, s.DefaultMethod,
               s.ChallengeOnNewDevice, s.ChallengeAfterFailedCharge,
               s.ChallengeUnauthenticatedCards, s.ChallengeAboveFarePence,
               s.ReverifyAfterDormantDays,
               s.MaxSavedCardsPerCustomer, s.MaxCardAddAttemptsPerDay,
               s.BlockBookingWhenDebtExceedsPence);

    private static DriverPaymentOptionsResponse ToResponse(Driver d, PaymentSettings s)
        => new(d.Id, d.AcceptsCashOverride, d.AcceptsCardOverride,
               DriverPaymentOptions.AcceptsCash(s, d), DriverPaymentOptions.AcceptsCard(s, d));
}
