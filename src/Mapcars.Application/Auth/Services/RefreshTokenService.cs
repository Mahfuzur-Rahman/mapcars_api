using System.Security.Cryptography;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Auth.Services;

/// <summary>
/// Issues, rotates and revokes refresh tokens — the mechanism behind "stay signed
/// in until you sign out".
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _tokens;
    private readonly ICustomerRepository _customers;
    private readonly IDriverRepository _drivers;
    private readonly IAdminRepository _admins;
    private readonly IJwtService _jwt;
    private readonly IUnitOfWork _uow;

    public RefreshTokenService(
        IRefreshTokenRepository tokens,
        ICustomerRepository customers,
        IDriverRepository drivers,
        IAdminRepository admins,
        IJwtService jwt,
        IUnitOfWork uow)
    {
        _tokens = tokens;
        _customers = customers;
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

    /// <summary>
    /// How long after a rotation the rotated-away token may come back without
    /// reading as theft. It covers a reply lost on a mobile network (the client
    /// retries on its next request) and two web requests renewing at once.
    /// Kept short on purpose: inside this window, someone holding a stolen copy
    /// could also renew unnoticed. They could already do that by using the
    /// stolen token before the real client did.
    /// </summary>
    public static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(120);

    public async Task<RefreshResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Your session has expired. Please sign in again.");

        var existing = await _tokens.GetByHashAsync(Hash(refreshToken), ct)
            ?? throw new UnauthorizedException("Your session has expired. Please sign in again.");

        // A token that was already rotated away is being presented again. Usually
        // that's theft: someone else got there first, and we can't tell who. So
        // every session for the account dies, and the real user signs in once
        // more. The one exception is the same client retrying moments later
        // because the reply to its first rotation never arrived.
        var isRetry = false;
        if (existing.RevokedAtUtc is not null)
        {
            if (!await IsRetryOfLostRotationAsync(existing, ct))
            {
                await RevokeAllAsync(existing.UserId, existing.UserType, ct);
                throw new UnauthorizedException("Your session was ended for security. Please sign in again.");
            }
            isRetry = true;
        }

        if (existing.IsExpired)
            throw new UnauthorizedException("Your session has expired. Please sign in again.");

        // Re-check the account on every refresh, not just at login. This is the
        // point where a deleted account stops being able to renew itself.
        var accessToken = await MintAccessTokenAsync(existing.UserId, existing.UserType, ct);
        if (accessToken is null)
        {
            // The account exists but may no longer sign in (a disabled admin).
            // It's refused exactly like a dead token, and the presented token
            // dies here so it can't be retried. A retry's token is already dead.
            if (!isRetry) existing.RevokedAtUtc = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);
            throw new UnauthorizedException("Your session has expired. Please sign in again.");
        }

        var replacement = NewToken();
        var replacementHash = Hash(replacement);

        // Rotate: the presented token dies and its successor takes over. On a
        // retry it's already dead, and its first successor is left alone. That
        // reply never arrived, so nobody holds it, and it expires unused.
        if (!isRetry)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            existing.ReplacedByTokenHash = replacementHash;
        }

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

    /// <summary>
    /// Whether a revoked token is its own client asking again for a rotation
    /// whose reply it never got. All three must hold:
    /// <list type="bullet">
    ///   <item>it was revoked BY ROTATION (logout and revoke-all leave
    ///   <see cref="RefreshToken.ReplacedByTokenHash"/> empty);</item>
    ///   <item>that was within <see cref="RotationGrace"/>;</item>
    ///   <item>its successor has never been used. A used successor proves the
    ///   reply arrived, so this is a genuine replay.</item>
    /// </list>
    /// </summary>
    private async Task<bool> IsRetryOfLostRotationAsync(RefreshToken presented, CancellationToken ct)
    {
        if (presented.ReplacedByTokenHash is null || presented.RevokedAtUtc is null) return false;
        if (DateTime.UtcNow - presented.RevokedAtUtc.Value > RotationGrace) return false;

        var successor = await _tokens.GetByHashAsync(presented.ReplacedByTokenHash, ct);
        return successor is not null && successor.IsActive;
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
    /// must not keep minting valid JWTs. Null when the account exists but may no
    /// longer sign in.
    /// </summary>
    private async Task<string?> MintAccessTokenAsync(Guid userId, string userType, CancellationToken ct)
    {
        // if/else rather than a switch: UserTypes.Customer and .LegacyCustomer hold
        // the same string until the rename cutover, and duplicate case labels are a
        // compile error. This is also the method that bounds the legacy window — it
        // reads the PERSISTED user_type and mints a fresh token with whatever the
        // deployed code calls that role, so once the column is rewritten every
        // renewal emits the new value and only in-flight access tokens carry the old.
        if (UserTypes.IsCustomer(userType))
        {
            var customer = await _customers.GetByIdAsync(userId, ct)
                ?? throw new UnauthorizedException("This account is no longer available.");
            return _jwt.GenerateUserToken(customer.Id, customer.Email ?? customer.PhoneNumber, UserTypes.Customer);
        }

        if (UserTypes.IsDriver(userType))
        {
            var driver = await _drivers.GetByIdAsync(userId, ct)
                ?? throw new UnauthorizedException("This account is no longer available.");
            return _jwt.GenerateUserToken(driver.Id, driver.Email ?? driver.PhoneNumber, UserTypes.Driver);
        }

        if (string.Equals(userType, UserTypes.Admin, StringComparison.OrdinalIgnoreCase))
        {
            // WithRole: the admin JWT carries a role claim, and the whole admin
            // portal authorises off it.
            var admin = await _admins.GetByIdWithRoleAsync(userId, ct)
                ?? throw new UnauthorizedException("This account is no longer available.");

            // Login refuses a disabled admin; so must refresh, or disabling one
            // only takes effect when their refresh token finally expires.
            return admin.IsActive ? _jwt.GenerateToken(admin) : null;
        }

        throw new UnauthorizedException("Your session has expired. Please sign in again.");
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
