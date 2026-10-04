using Mapcars.Application.Auth.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Auth;

/// <summary>
/// The step every Google/Apple sign-in takes before linking itself to an account
/// it found by the provider-verified email.
/// <para>
/// If that account's email was never verified, nothing on the row proves its
/// creator owns the mailbox: it may be an attacker who signed up with the
/// victim's address and their own password, waiting for the victim to arrive by
/// Google or Apple. Linking alone would hand the victim an account the attacker
/// can still sign in to — so the password and every existing session go first.
/// The owner can set a password later through reset. An account whose email was
/// already verified belongs to whoever proved the mailbox, and is left alone.
/// </para>
/// <para>
/// Then the address is marked verified, because the provider vouched for it —
/// and because an unverified row is one the email sign-up form treats as an
/// abandoned registration it may overwrite with a new password.
/// </para>
/// <para>
/// Revocation happens here, before the caller issues the new session, so only
/// that one survives. Customers and drivers share no base type, hence the twin
/// overloads — keep them identical.
/// </para>
/// </summary>
public static class SocialAccountLink
{
    public static async Task ClaimAsync(Customer customer, IRefreshTokenService refreshTokens, CancellationToken ct)
    {
        if (!customer.IsEmailVerified)
        {
            customer.PasswordHash = null;
            await refreshTokens.RevokeAllAsync(customer.Id, UserTypes.Customer, ct);
        }

        customer.IsEmailVerified = true;
    }

    public static async Task ClaimAsync(Driver driver, IRefreshTokenService refreshTokens, CancellationToken ct)
    {
        if (!driver.IsEmailVerified)
        {
            driver.PasswordHash = null;
            await refreshTokens.RevokeAllAsync(driver.Id, UserTypes.Driver, ct);
        }

        driver.IsEmailVerified = true;
    }

    /// <summary>
    /// For an account the provider found by its own sub (so it needs no claiming):
    /// if the provider vouches for the very address on the row, mark it verified —
    /// that is the way back for a user whose profile edit reset the flag. A
    /// different address proves nothing about this row and changes nothing, which
    /// is what stops someone aiming their own linked account at a victim's email.
    /// Returns whether the row changed, so the caller knows to save.
    /// </summary>
    public static bool ConfirmEmail(Customer customer, string? providerVerifiedEmail)
    {
        if (customer.IsEmailVerified || !SameAddress(customer.Email, providerVerifiedEmail)) return false;
        customer.IsEmailVerified = true;
        return true;
    }

    public static bool ConfirmEmail(Driver driver, string? providerVerifiedEmail)
    {
        if (driver.IsEmailVerified || !SameAddress(driver.Email, providerVerifiedEmail)) return false;
        driver.IsEmailVerified = true;
        return true;
    }

    /// <summary>
    /// True when the row belongs to someone: tied to a Google/Apple identity or a
    /// phone that passed OTP. Such a row is never an abandoned sign-up, whatever its
    /// email flag says — a profile edit can reset that flag on a live account.
    /// </summary>
    public static bool HasEstablishedIdentity(Customer customer)
        => customer.GoogleSub is not null || customer.AppleSub is not null || customer.IsPhoneVerified;

    public static bool HasEstablishedIdentity(Driver driver)
        => driver.GoogleSub is not null || driver.AppleSub is not null || driver.IsPhoneVerified;

    private static bool SameAddress(string? rowEmail, string? providerEmail)
        => !string.IsNullOrWhiteSpace(rowEmail) && !string.IsNullOrWhiteSpace(providerEmail)
           && string.Equals(rowEmail, providerEmail.Trim(), StringComparison.OrdinalIgnoreCase);
}
