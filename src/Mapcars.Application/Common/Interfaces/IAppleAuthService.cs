namespace Mapcars.Application.Common.Interfaces;

public class AppleUserInfo
{
    /// <summary>Apple's stable, app-scoped user id. The identity — never the email.</summary>
    public string Sub { get; set; } = string.Empty;

    /// <summary>Only present the first time a user authorises the app, and may be a private relay address.</summary>
    public string? Email { get; set; }

    public bool EmailVerified { get; set; }

    /// <summary>An @privaterelay.appleid.com forwarding address ("Hide My Email").</summary>
    public bool IsPrivateEmail { get; set; }
}

/// <summary>
/// Verifies Apple identity tokens for "Sign in with Apple" — required by App
/// Review (guideline 4.8) for any iOS app that offers Google sign-in.
/// </summary>
public interface IAppleAuthService
{
    bool IsConfigured { get; }

    /// <summary>
    /// Validates signature (Apple's published keys), issuer, expiry, audience AND
    /// nonce. <paramref name="rawNonce"/> is the value the app generated; the
    /// token must carry its SHA-256 hex digest — that is what makes a captured
    /// token useless to replay. Null when anything fails.
    /// </summary>
    Task<AppleUserInfo?> VerifyIdTokenAsync(string idToken, string rawNonce, CancellationToken ct = default);
}
