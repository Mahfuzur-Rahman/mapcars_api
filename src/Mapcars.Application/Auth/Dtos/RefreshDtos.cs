namespace Mapcars.Application.Auth.Dtos;

/// <summary>Exchange a refresh token for a fresh access token.</summary>
public record RefreshRequest(string RefreshToken);

/// <summary>Sign out one device. Revokes the refresh token server-side, so a copy
/// of it taken off the device is dead too.</summary>
public record LogoutRequest(string RefreshToken);

/// <summary>
/// The result of a refresh. Carries a **new** refresh token as well as the access
/// token: tokens are rotated on every use, so the client must store this one and
/// discard the one it sent.
/// </summary>
public class RefreshResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }

    /// <summary>The rotated successor. Store it; the token you sent is now dead.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>"rider" | "driver" | "admin" — lets a client confirm the session
    /// it restored is the role it expects.</summary>
    public string UserType { get; set; } = string.Empty;

    public Guid UserId { get; set; }
}
