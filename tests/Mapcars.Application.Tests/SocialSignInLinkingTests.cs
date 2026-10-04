using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Services;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Services;
using Mapcars.Application.Drivers.Services;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Tests;

/// <summary>
/// The pre-registration takeover: an attacker signs up with the victim's email
/// and their own password, and never verifies it. When the victim later signs in
/// with Google/Apple, that row is found by email and linked to the victim. If the
/// attacker's password (or a session) survives the link, the attacker is inside
/// the victim's account.
///
/// Run against every social path that links by email — both apps × Google/Apple,
/// plus the web's unified Google sign-in.
/// </summary>
public class SocialSignInLinkingTests
{
    private const string Victim = "victim@example.com";
    private const string PreExistingHash = "pre-existing-hash";

    public enum SignInPath { CustomerGoogle, CustomerApple, DriverGoogle, DriverApple, UnifiedCustomerGoogle, UnifiedDriverGoogle }

    public static TheoryData<SignInPath> AllPaths()
    {
        var data = new TheoryData<SignInPath>();
        foreach (var path in Enum.GetValues<SignInPath>()) data.Add(path);
        return data;
    }

    private static readonly GoogleUserInfo Google = new() { Sub = "google-sub", Email = Victim, EmailVerified = true, Name = "Victim" };
    private static readonly AppleUserInfo Apple = new() { Sub = "apple-sub", Email = Victim, EmailVerified = true };

    /// <summary>The account as it stands after the social sign-in, plus what was asked of the token store.</summary>
    private sealed record Outcome(
        Guid AccountId, string UserType, string? PasswordHash, bool IsEmailVerified, string? LinkedSub, SpyRefreshTokens Tokens);

    private static async Task<Outcome> SignInAsync(SignInPath path, bool emailVerifiedBefore)
    {
        var spy = new SpyRefreshTokens();
        var uow = new FakeUnitOfWork();
        var jwt = new FakeJwt();

        var isCustomer = path is SignInPath.CustomerGoogle or SignInPath.CustomerApple or SignInPath.UnifiedCustomerGoogle;
        var customer = isCustomer
            ? new Customer { Email = Victim, PasswordHash = PreExistingHash, IsEmailVerified = emailVerifiedBefore, IsActive = true }
            : null;
        var driver = isCustomer
            ? null
            : new Driver { Email = Victim, PasswordHash = PreExistingHash, IsEmailVerified = emailVerifiedBefore };
        var customers = new FakeCustomerRepo(customer);
        var drivers = new FakeDriverRepo(driver);

        // Dependencies these paths never reach (OTP, hasher, storage, env, geo,
        // settings, admins) are passed as null: touching one fails the test.
        switch (path)
        {
            case SignInPath.CustomerGoogle:
                await new CustomerAuthService(customers, null!, null!, new FakeGoogle(Google), null!, jwt, spy, null!, null!, uow)
                    .SignInWithGoogleAsync("id-token");
                break;
            case SignInPath.CustomerApple:
                await new CustomerAuthService(customers, null!, null!, null!, new FakeApple(Apple), jwt, spy, null!, null!, uow)
                    .SignInWithAppleAsync(new AppleAuthRequest("id-token", "nonce"));
                break;
            case SignInPath.DriverGoogle:
                await new DriverAuthService(drivers, null!, null!, new FakeGoogle(Google), null!, jwt, spy, null!, null!, null!, null!, uow)
                    .SignInWithGoogleAsync("id-token");
                break;
            case SignInPath.DriverApple:
                await new DriverAuthService(drivers, null!, null!, null!, new FakeApple(Apple), jwt, spy, null!, null!, null!, null!, uow)
                    .SignInWithAppleAsync(new AppleAuthRequest("id-token", "nonce"));
                break;
            case SignInPath.UnifiedCustomerGoogle:
            case SignInPath.UnifiedDriverGoogle:
                await new UnifiedAuthService(null!, customers, drivers, null!, jwt, spy, new FakeGoogle(Google), uow)
                    .GoogleLoginAsync(new UnifiedGoogleLoginRequest("id-token"));
                break;
        }

        return customer is not null
            ? new Outcome(customer.Id, UserTypes.Customer, customer.PasswordHash, customer.IsEmailVerified,
                customer.GoogleSub ?? customer.AppleSub, spy)
            : new Outcome(driver!.Id, UserTypes.Driver, driver.PasswordHash, driver.IsEmailVerified,
                driver.GoogleSub ?? driver.AppleSub, spy);
    }

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Linking_an_unverified_account_wipes_its_password(SignInPath path)
    {
        var after = await SignInAsync(path, emailVerifiedBefore: false);

        Assert.NotNull(after.LinkedSub);
        Assert.Null(after.PasswordHash);
    }

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Linking_an_unverified_account_ends_every_existing_session_but_the_new_one(SignInPath path)
    {
        var after = await SignInAsync(path, emailVerifiedBefore: false);

        Assert.Equal([(after.AccountId, after.UserType)], after.Tokens.RevokedAll);
        // Revoked BEFORE the social user's own session is issued, so it survives.
        Assert.Equal(["revokeAll", "issue"], after.Tokens.Calls.Select(c => c.Call));
    }

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Linking_an_unverified_account_marks_the_email_verified(SignInPath path)
    {
        // The provider vouched for the address. Left unverified, the email
        // sign-up form would still treat the row as an abandoned registration and
        // let anyone set a new password on it — reopening the same hole.
        var after = await SignInAsync(path, emailVerifiedBefore: false);

        Assert.True(after.IsEmailVerified);
    }

    [Theory]
    [MemberData(nameof(AllPaths))]
    public async Task Linking_a_verified_account_leaves_its_password_and_sessions_alone(SignInPath path)
    {
        // The owner proved the mailbox before: the password is theirs, and so are
        // their other devices' sessions.
        var after = await SignInAsync(path, emailVerifiedBefore: true);

        Assert.NotNull(after.LinkedSub);
        Assert.Equal(PreExistingHash, after.PasswordHash);
        Assert.Empty(after.Tokens.RevokedAll);
        Assert.True(after.IsEmailVerified);
    }
}
