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
}
