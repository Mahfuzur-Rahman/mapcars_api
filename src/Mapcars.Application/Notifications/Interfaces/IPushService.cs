using Mapcars.Application.Notifications.Dtos;

namespace Mapcars.Application.Notifications.Interfaces;

/// <summary>
/// High-level push notifications: register/unregister a device, and notify a
/// rider/driver by looking up their tokens and sending (pruning dead ones).
/// All sends are best-effort — a push failure never breaks the caller.
/// </summary>
public interface IPushService
{
    Task RegisterAsync(string userType, Guid userId, RegisterDeviceRequest request, CancellationToken ct = default);

    Task UnregisterAsync(string token, CancellationToken ct = default);

    /// <summary>Send a push to every device of one user (best-effort).</summary>
    Task NotifyUserAsync(string userType, Guid userId, PushMessage message, CancellationToken ct = default);
}
