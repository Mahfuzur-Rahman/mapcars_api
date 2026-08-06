using Google.Apis.Auth;
using Mapcars.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Verifies Google ID tokens for "Continue with Google".
///
/// <para><b>Audience validation is mandatory.</b> A Google ID token only proves
/// the bearer signed in to <i>some</i> Google OAuth client — the <c>aud</c>
/// claim is what says it was <i>ours</i>. Skip that check and a token minted by
/// any other app on the internet would authenticate its holder as that Google
/// account's owner here. So this service <b>fails closed</b>: with no client ID
/// configured it rejects every token instead of validating without an audience.
/// </para>
///
/// <para>Each platform gets its own OAuth client (Web, Android, iOS) and each
/// mints tokens carrying its own <c>aud</c>, so every one of them must be
/// listed. Configure either a comma-separated <c>Google:ClientId</c> or a
/// <c>Google:ClientIds</c> array — as env vars, <c>Google__ClientId</c> /
/// <c>Google__ClientIds__0</c>.</para>
/// </summary>
public class GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> logger)
    : IGoogleAuthService
{
    private readonly IReadOnlyList<string> _clientIds = ReadClientIds(config);

    public bool IsConfigured => _clientIds.Count > 0;

    public async Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            // Only reachable if a caller skipped the IsConfigured check.
            logger.LogError(
                "Rejecting Google sign-in: no Google:ClientId is configured, so the token's "
                + "audience cannot be verified. Set Google__ClientId to the OAuth client ID(s).");
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _clientIds,
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserInfo
            {
                Sub = payload.Subject,
                Email = payload.Email ?? string.Empty,
                Name = payload.Name ?? string.Empty,
                EmailVerified = payload.EmailVerified,
            };
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Google token validation failed: {Message}", ex.Message);
            return null;
        }
    }

    /// Accepts a single ID, a comma-separated list, and/or a `ClientIds` array.
    private static IReadOnlyList<string> ReadClientIds(IConfiguration config)
    {
        var ids = new List<string>();

        var single = config["Google:ClientId"];
        if (!string.IsNullOrWhiteSpace(single))
        {
            ids.AddRange(single.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (var child in config.GetSection("Google:ClientIds").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value)) ids.Add(child.Value.Trim());
        }

        return ids.Distinct(StringComparer.Ordinal).ToList();
    }
}
