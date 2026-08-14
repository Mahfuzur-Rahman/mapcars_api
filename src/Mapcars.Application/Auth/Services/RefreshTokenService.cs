using System.Security.Cryptography;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Auth.Services;

/// <summary>
/// Issues, rotates and revokes refresh tokens — the mechanism behind "stay signed
/// in until you sign out".
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _tokens;
    private readonly IRiderRepository _riders;
    private readonly IDriverRepository _drivers;
    private readonly IAdminRepository _admins;
    private readonly IJwtService _jwt;
    private readonly IUnitOfWork _uow;

    public RefreshTokenService(
        IRefreshTokenRepository tokens,
        IRiderRepository riders,
        IDriverRepository drivers,
        IAdminRepository admins,
        IJwtService jwt,
        IUnitOfWork uow)
    {
        _tokens = tokens;
        _riders = riders;
        _drivers = drivers;
        _admins = admins;
        _jwt = jwt;
        _uow = uow;
    }

    public async Task<string> IssueAsync(
        Guid userId, string userType, string? deviceLabel = null, CancellationToken ct = default)
    {
        var raw = NewToken();

        await _tokens.AddAsync(new RefreshToken
        {
            UserId = userId,
            UserType = userType,
            TokenHash = Hash(raw),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            DeviceLabel = Trim(deviceLabel, 120),
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RefreshResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Your session has expired. Please sign in again.");

        var existing = await _tokens.GetByHashAsync(Hash(refreshToken), ct)
            ?? throw new UnauthorizedException("Your session has expired. Please sign in again.");

        // A token that was already rotated away is being presented again. Either
        // this client raced itself, or the token was stolen and someone else got
        // there first — and we can't tell which. Treat it as theft and kill every
        // session for the account; the legitimate user signs in once more, the
        // thief's freshly-rotated token dies with the rest.
        if (existing.RevokedAtUtc is not null)
        {
            await RevokeAllAsync(existing.UserId, existing.UserType, ct);
            throw new UnauthorizedException("Your session was ended for security. Please sign in again.");
        }

        if (existing.IsExpired)
            throw new UnauthorizedException("Your session has expired. Please sign in again.");

        // Re-check the account on every refresh, not just at login. This is the
        // point where a deleted account stops being able to renew itself.
        var accessToken = await MintAccessTokenAsync(existing.UserId, existing.UserType, ct);

        // Rotate: the presented token dies, its successor takes over.
        var replacement = NewToken();
        var replacementHash = Hash(replacement);

        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.ReplacedByTokenHash = replacementHash;

        await _tokens.AddAsync(new RefreshToken
        {
            UserId = existing.UserId,
            UserType = existing.UserType,
            TokenHash = replacementHash,
            // Sliding window: an app in daily use never expires. Deliberate —
            // that is what "stay signed in" means.
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            DeviceLabel = existing.DeviceLabel,
        }, ct);

        await _uow.SaveChangesAsync(ct);

        return new RefreshResponse
        {
            Token = accessToken,
            ExpiresInMinutes = _jwt.ExpiryMinutes,
            RefreshToken = replacement,
            UserType = existing.UserType,
            UserId = existing.UserId,
        };
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var existing = await _tokens.GetByHashAsync(Hash(refreshToken), ct);
        // Unknown or already revoked: signing out twice is not an error.
        if (existing is null || existing.RevokedAtUtc is not null) return;

        existing.RevokedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(Guid userId, string userType, CancellationToken ct = default)
    {
        var active = await _tokens.ListActiveForUserAsync(userId, userType, ct);
        if (active.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var token in active) token.RevokedAtUtc = now;

        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Mints the access token for whichever kind of account this is. Loading the
    /// account also proves it still exists — a refresh token outliving its user
    /// must not keep minting valid JWTs.
    /// </summary>
    private async Task<string> MintAccessTokenAsync(Guid userId, string userType, CancellationToken ct)
    {
        switch (userType)
        {
            case "rider":
                var rider = await _riders.GetByIdAsync(userId, ct)
                    ?? throw new UnauthorizedException("This account is no longer available.");
                return _jwt.GenerateUserToken(rider.Id, rider.Email ?? rider.PhoneNumber, "rider");

            case "driver":
                var driver = await _drivers.GetByIdAsync(userId, ct)
                    ?? throw new UnauthorizedException("This account is no longer available.");
                return _jwt.GenerateUserToken(driver.Id, driver.Email ?? driver.PhoneNumber, "driver");

            case "admin":
                // WithRole: the admin JWT carries a role claim, and the whole admin
                // portal authorises off it.
                var admin = await _admins.GetByIdWithRoleAsync(userId, ct)
                    ?? throw new UnauthorizedException("This account is no longer available.");
                return _jwt.GenerateToken(admin);

            default:
                throw new UnauthorizedException("Your session has expired. Please sign in again.");
        }
    }

    /// <summary>256 bits from a cryptographic RNG, url-safe. Not a JWT and not
    /// guessable — it is only ever compared against a stored hash.</summary>
    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>
    /// SHA-256, hex. No salt and no work factor on purpose: unlike a password this
    /// is 256 bits of entropy we generated ourselves, so there is nothing to brute
    /// force, and refresh runs on the hot path where bcrypt would be felt.
    /// </summary>
    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string? Trim(string? value, int max)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= max ? value : value[..max];
}
