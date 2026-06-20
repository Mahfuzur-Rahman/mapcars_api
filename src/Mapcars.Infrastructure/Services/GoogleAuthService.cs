using Google.Apis.Auth;
using Mapcars.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

public class GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> logger) : IGoogleAuthService
{
    public async Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            var clientId = config["Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrEmpty(clientId))
                settings.Audience = [clientId];

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
}
