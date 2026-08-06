using Mapcars.Domain.Entities;

namespace Mapcars.Application.Notifications.Interfaces;

public interface IDeviceTokenRepository
{
    /// <summary>Insert the token, or move it to this owner if it already exists.</summary>
    Task UpsertAsync(DeviceToken token, CancellationToken ct = default);

    Task RemoveByTokenAsync(string token, CancellationToken ct = default);

    Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct = default);

    /// <summary>The FCM tokens registered to one owner (rider/driver).</summary>
    Task<IReadOnlyList<string>> ListTokensForUserAsync(
        string userType, Guid userId, CancellationToken ct = default);
}
