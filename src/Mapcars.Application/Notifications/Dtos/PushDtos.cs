namespace Mapcars.Application.Notifications.Dtos;

/// <summary>Client registers its FCM token after sign-in (and on token refresh).</summary>
public record RegisterDeviceRequest(string Token, string? Platform);

/// <summary>A push payload: a visible title/body plus optional data for in-app routing.</summary>
public record PushMessage(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);
