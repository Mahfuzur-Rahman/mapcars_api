using Mapcars.Application.Auth.Dtos;

namespace Mapcars.Application.Auth.Interfaces;

/// <summary>
/// Issues and rotates the long-lived credential that keeps a user signed in.
/// <para>
/// The access token (JWT) stays deliberately short-lived because nothing can
/// revoke it once minted. The refresh token is the opposite: long-lived, stored
/// server-side (hashed), and revocable — which is what makes "log out" and
/// "ban this driver" mean something on a device that is already holding a token.
/// </para>
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Mints a new refresh token for a user who has just proven who they are
    /// (password, OTP or Google). The raw value is returned here and nowhere
    /// else — only its hash is stored.
    /// </summary>
    Task<string> IssueAsync(
        Guid userId, string userType, string? deviceLabel = null, CancellationToken ct = default);

    /// <summary>
    /// Exchanges a refresh token for a fresh access token and a **new** refresh
    /// token (rotation). Throws <c>UnauthorizedException</c> if the token is
    /// unknown, expired, or already used — the last case being a theft signal,
    /// which revokes every sibling session for that user.
    /// </summary>
    Task<RefreshResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes one token — the sign-out path. Silent if it's already
    /// gone: signing out twice isn't an error worth surfacing.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes every active session for a user ("sign out everywhere",
    /// and the hook for suspending an account).</summary>
    Task RevokeAllAsync(Guid userId, string userType, CancellationToken ct = default);
}
