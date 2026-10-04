namespace Mapcars.Application.Common.Interfaces;

public interface IOtpService
{
    /// <summary>The <c>verification_codes.provider</c> value for password-reset codes (migration 039).</summary>
    const string ResetProvider = "reset";

    /// <summary>Guesses compared against one code before it locks.</summary>
    const int MaxAttempts = 5;

    Task<string> CreateAndSendPhoneOtpAsync(string userType, string phone, CancellationToken ct = default);
    Task<string> CreateAndSendEmailOtpAsync(string userType, string email, CancellationToken ct = default);

    /// <summary>Emails a password-reset code. Kept apart from the sign-up email code (its own provider).</summary>
    Task CreateAndSendPasswordResetAsync(string userType, string email, CancellationToken ct = default);

    /// <summary>
    /// Checks <paramref name="code"/> against the live code for this identifier and
    /// consumes it on success. Every guess counts, atomically, so parallel guesses
    /// can't outrun the cap; after <see cref="MaxAttempts"/> even the right one fails.
    /// </summary>
    Task<bool> VerifyAsync(string userType, string provider, string identifier, string code, CancellationToken ct = default);
}
