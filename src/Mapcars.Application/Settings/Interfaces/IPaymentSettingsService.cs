using Mapcars.Application.Settings.Dtos;

namespace Mapcars.Application.Settings.Interfaces;

public interface IPaymentSettingsService
{
    Task<PaymentSettingsResponse> GetAsync(CancellationToken ct = default);

    Task<PaymentSettingsResponse> UpdateAsync(
        UpdatePaymentSettingsRequest request, Guid adminId, CancellationToken ct = default);

    Task<DriverPaymentOptionsResponse> GetDriverOptionsAsync(Guid driverId, CancellationToken ct = default);

    Task<DriverPaymentOptionsResponse> UpdateDriverOptionsAsync(
        Guid driverId, UpdateDriverPaymentOptionsRequest request, CancellationToken ct = default);
}
