using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Auth.Services;

/// <summary>
/// Forgot-password by emailed code. The code proves control of the mailbox, which
/// is exactly what account ownership rests on — so a Google-only account (no
/// password yet) may use this to set one, the same way a sign-up verifies email.
/// </summary>
public class PasswordResetService(
    ICustomerRepository customers,
    IDriverRepository drivers,
    IOtpService otp,
    IPasswordHasher hasher,
    IUnitOfWork uow,
    IRefreshTokenService refreshTokens) : IPasswordResetService
{
    /// <summary>One message for every failure, so a wrong code, a burnt code and a
    /// non-existent account are indistinguishable from outside.</summary>
    private const string InvalidCode = "That code is invalid or has expired. Please request a new one.";

    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var userType = UserTypes.Canonical(request.UserType);
        var email = Normalise(request.Email);

        if (await FindAsync(userType, email, ct) is null) return;

        await otp.CreateAndSendPasswordResetAsync(userType, email, ct);
    }

    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var userType = UserTypes.Canonical(request.UserType);
        var email = Normalise(request.Email);

        // Verify first: it is what counts (and caps) the guesses, whether or not
        // the account exists.
        if (!await otp.VerifyAsync(userType, IOtpService.ResetProvider, email, request.Code.Trim(), ct))
            throw new DomainException(InvalidCode);

        var account = await FindAsync(userType, email, ct) ?? throw new DomainException(InvalidCode);

        account.SetPasswordHash(hasher.Hash(request.NewPassword));
        await uow.SaveChangesAsync(ct);

        // Whoever had the old password may still hold a session. A reset that
        // leaves them signed in hasn't locked anybody out.
        await refreshTokens.RevokeAllAsync(account.Id, userType, ct);
    }

    private async Task<Account?> FindAsync(string userType, string email, CancellationToken ct)
    {
        if (UserTypes.IsDriver(userType))
        {
            var driver = await drivers.FindByEmailAsync(email, ct);
            return driver is null ? null : new Account(driver.Id, h => driver.PasswordHash = h);
        }

        var customer = await customers.FindByEmailAsync(email, ct);
        return customer is null ? null : new Account(customer.Id, h => customer.PasswordHash = h);
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();

    /// <summary>The two account kinds share nothing but these two facts.</summary>
    private sealed record Account(Guid Id, Action<string> SetPasswordHash);
}
