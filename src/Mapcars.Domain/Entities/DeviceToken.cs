using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A push-notification registration token for one app install. Owned by a rider
/// or a driver (<see cref="UserType"/> + <see cref="UserId"/>). The FCM token is
/// unique per install and can move between users on shared devices, so it's keyed
/// on <see cref="Token"/> (upserted) rather than the user.
/// </summary>
public class DeviceToken : BaseEntity
{
    /// <summary>"rider" | "driver" — matches the JWT role of the owner.</summary>
    public required string UserType { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The FCM registration token for this install.</summary>
    public required string Token { get; set; }

    /// <summary>"android" | "ios" (informational).</summary>
    public string? Platform { get; set; }
}
