using Mapcars.Application.Auth.Dtos;

namespace Mapcars.Application.Auth.Interfaces;

public interface IUnifiedAuthService
{
    /// <summary>
    /// Looks up the email across Admin, Rider, and Driver accounts and logs
    /// into whichever one the password actually matches.
    /// </summary>
    Task<UnifiedLoginResponse> LoginAsync(UnifiedLoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// Verifies a Google ID token and logs into the matching Rider or Driver account,
    /// or presents a choice if both exist under the verified email / Google sub.
    /// </summary>
    Task<UnifiedLoginResponse> GoogleLoginAsync(UnifiedGoogleLoginRequest request, CancellationToken ct = default);
}
