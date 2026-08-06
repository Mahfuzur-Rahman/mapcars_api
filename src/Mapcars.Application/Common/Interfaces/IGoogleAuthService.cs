namespace Mapcars.Application.Common.Interfaces;

public class GoogleUserInfo
{
    public string Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
}

public interface IGoogleAuthService
{
    /// <summary>
    /// True when at least one OAuth client ID is configured. Without one an ID
    /// token's audience can't be verified, so Google sign-in is refused
    /// outright — check this first to tell the user *why* rather than returning
    /// a misleading "invalid token".
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Validates the token's signature, issuer, expiry **and audience**.
    /// Returns null when the token is invalid or the service is unconfigured.
    /// </summary>
    Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken ct = default);
}
