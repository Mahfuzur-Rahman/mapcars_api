using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Common;

/// <summary>
/// The rules behind a one-time code (sign-up, phone login and password reset all
/// share them). Kept free of EF so the decisions can be tested on their own; the
/// store that applies them is <c>OtpService</c> in Infrastructure.
/// </summary>
public static class OtpCode
{
    /// <summary>
    /// A fresh 6-digit code, 100000–999999 — the same shape the codes always had
    /// (the column is VARCHAR(6), and there is never a leading zero to lose).
    /// From the cryptographic RNG: <c>Random</c> is predictable from its own
    /// output, and a reset code is a key to the whole account.
    /// </summary>
    public static string Generate()
        => RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Compares a guess in constant time, so response timing can't reveal how
    /// many leading digits were right. A length mismatch is answered at once —
    /// that only leaks the length, which is public anyway.
    /// </summary>
    public static bool Matches(string expected, string? supplied)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied ?? string.Empty));

    /// <summary>
    /// Whether a code may still have a guess compared against it: live (unused,
    /// unexpired) and under <see cref="IOtpService.MaxAttempts"/>.
    /// <para>
    /// An expression rather than a method because the store puts it in the
    /// <c>WHERE</c> of the conditional UPDATE that bumps the attempt counter. The
    /// database evaluates check-and-increment as one statement, so parallel
    /// guesses can't all read the same count and all slip under the cap — which
    /// is exactly what reading the count, incrementing it in memory and saving
    /// allowed.
    /// </para>
    /// </summary>
    public static Expression<Func<VerificationCode, bool>> MayAttempt(DateTime now)
        => v => v.UsedAt == null && v.ExpiresAt > now && v.AttemptCount < IOtpService.MaxAttempts;
}
