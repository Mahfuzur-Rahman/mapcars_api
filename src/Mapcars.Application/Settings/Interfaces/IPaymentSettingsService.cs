using Mapcars.Application.Settings.Dtos;

namespace Mapcars.Application.Settings.Interfaces;

public interface IPaymentSettingsService
{
    /// <summary>The public shape — payment methods only, no fraud thresholds.</summary>
    Task<PaymentSettingsResponse> GetAsync(CancellationToken ct = default);

    /// <summary>The full document, for the admin portal.</summary>
    Task<AdminPaymentSettingsResponse> GetForAdminAsync(CancellationToken ct = default);

    Task<AdminPaymentSettingsResponse> UpdateAsync(
        UpdatePaymentSettingsRequest request, Guid adminId, CancellationToken ct = default);

    Task<DriverPaymentOptionsResponse> GetDriverOptionsAsync(Guid driverId, CancellationToken ct = default);

    Task<DriverPaymentOptionsResponse> UpdateDriverOptionsAsync(
        Guid driverId, UpdateDriverPaymentOptionsRequest request, CancellationToken ct = default);
}
