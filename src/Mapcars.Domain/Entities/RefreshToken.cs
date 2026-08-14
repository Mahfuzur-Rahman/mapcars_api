using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A long-lived credential that mints short-lived access tokens, so a signed-in
/// user stays signed in until they sign out.
/// <para>
/// Never holds the token itself — only <see cref="TokenHash"/> (SHA-256). The raw
/// value is returned to the client exactly once, at issue time, and is
/// unrecoverable afterwards; a stolen database is therefore not a set of working
/// sessions.
/// </para>
/// <para>
/// Rotated on every use: refreshing revokes the presented token and issues a
/// successor, linked by <see cref="ReplacedByTokenHash"/>. That chain is what
/// makes theft detectable — see <see cref="IsActive"/> and the reuse check in
/// <c>RefreshTokenService</c>.
/// </para>
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>Rider, driver or admin id. Scoped by <see cref="UserType"/>,
    /// since those ids live in separate tables and could collide.</summary>
    public Guid UserId { get; set; }

    /// <summary>"rider" | "driver" | "admin".</summary>
    public required string UserType { get; set; }

    /// <summary>SHA-256 of the token, hex-encoded.</summary>
    public required string TokenHash { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set when the token is rotated away or explicitly revoked (logout).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>The successor issued when this token was rotated. Null for a token
    /// that was revoked outright, or is still current.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Device name for a future "active sessions" screen. Optional.</summary>
    public string? DeviceLabel { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    /// <summary>Usable right now: neither revoked nor past its expiry.</summary>
    public bool IsActive => RevokedAtUtc is null && !IsExpired;
}
