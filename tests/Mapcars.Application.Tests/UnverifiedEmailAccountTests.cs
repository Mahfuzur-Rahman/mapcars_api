using System.Linq.Expressions;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Services;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Customers.Services;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Drivers.Services;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Tests;

/// <summary>
/// Once a profile email change resets <c>IsEmailVerified</c>, an account that
/// really belongs to someone (Google/Apple linked, or a verified phone) can sit
/// with an unverified email. Two things must hold for such an account:
/// <list type="bullet">
/// <item>the provider that identifies it still signs it in — a Google-only user has
/// no other way back in;</item>
/// <item>email sign-up must not treat it as an abandoned registration to overwrite,
/// or anyone who knows the new address could set the password the owner is then
/// invited to verify.</item>
/// </list>
/// </summary>
public class UnverifiedEmailAccountTests
{
    private const string RowEmail = "new-address@example.com";
    private const string OtherEmail = "google-login@example.com";
    private const string OldHash = "old-hash";
    private const string AlreadyExists = "An account with this email already exists. Please log in instead.";

    // ── Social sign-in by provider sub ────────────────────────────────────────

    public enum SubPath { UnifiedCustomerGoogle, UnifiedDriverGoogle, CustomerGoogle, CustomerApple, DriverGoogle, DriverApple }

    public static TheoryData<SubPath> AllSubPaths()
    {
        var data = new TheoryData<SubPath>();
        foreach (var path in Enum.GetValues<SubPath>()) data.Add(path);
        return data;
    }

    /// <summary>
    /// Signs in the account found by its sub (email unverified, no password) and
    /// returns whether it worked plus the flag afterwards. <paramref name="providerEmail"/>
    /// is what the provider's token says, with <paramref name="providerVerified"/>.
    /// </summary>
    private static async Task<(bool SignedIn, bool IsEmailVerified)> SignInBySubAsync(
        SubPath path, string providerEmail, bool providerVerified = true)
    {
        var spy = new SpyRefreshTokens();
        var uow = new FakeUnitOfWork();
        var jwt = new FakeJwt();
        var google = new FakeGoogle(new GoogleUserInfo { Sub = "g-sub", Email = providerEmail, EmailVerified = providerVerified, Name = "N" });
        var apple = new FakeApple(new AppleUserInfo { Sub = "a-sub", Email = providerEmail, EmailVerified = providerVerified });

        var isCustomer = path is SubPath.UnifiedCustomerGoogle or SubPath.CustomerGoogle or SubPath.CustomerApple;
        var customer = isCustomer
            ? new Customer { Email = RowEmail, GoogleSub = "g-sub", AppleSub = "a-sub", IsEmailVerified = false, IsActive = true }
            : null;
        var driver = isCustomer ? null : new Driver { Email = RowEmail, GoogleSub = "g-sub", AppleSub = "a-sub", IsEmailVerified = false };
        var customers = new SubCustomerRepo(customer);
        var drivers = new SubDriverRepo(driver);

        switch (path)
        {
            case SubPath.UnifiedCustomerGoogle:
            case SubPath.UnifiedDriverGoogle:
                await new UnifiedAuthService(null!, customers, drivers, null!, jwt, spy, google, uow)
                    .GoogleLoginAsync(new UnifiedGoogleLoginRequest("id-token"));
                break;
            case SubPath.CustomerGoogle:
                await new CustomerAuthService(customers, null!, null!, google, null!, jwt, spy, null!, null!, uow)
                    .SignInWithGoogleAsync("id-token");
                break;
            case SubPath.CustomerApple:
                await new CustomerAuthService(customers, null!, null!, null!, apple, jwt, spy, null!, null!, uow)
                    .SignInWithAppleAsync(new AppleAuthRequest("id-token", "nonce"));
                break;
            case SubPath.DriverGoogle:
                await new DriverAuthService(drivers, null!, null!, google, null!, jwt, spy, null!, null!, null!, null!, uow)
                    .SignInWithGoogleAsync("id-token");
                break;
            case SubPath.DriverApple:
                await new DriverAuthService(drivers, null!, null!, null!, apple, jwt, spy, null!, null!, null!, null!, uow)
                    .SignInWithAppleAsync(new AppleAuthRequest("id-token", "nonce"));
                break;
        }

        var verified = customer?.IsEmailVerified ?? driver!.IsEmailVerified;
        return (spy.Calls.Any(c => c.Call == "issue"), verified);
    }

    [Theory]
    [MemberData(nameof(AllSubPaths))]
    public async Task Linked_account_with_unverified_email_still_signs_in_with_its_provider(SubPath path)
    {
        // The provider's address differs from the row's (the user edited their
        // profile email), so it proves nothing about the row: stays unverified.
        var (signedIn, verified) = await SignInBySubAsync(path, OtherEmail);

        Assert.True(signedIn);
        Assert.False(verified);
    }

    [Theory]
    [MemberData(nameof(AllSubPaths))]
    public async Task Provider_vouching_for_the_rows_own_email_verifies_it(SubPath path)
    {
        var (signedIn, verified) = await SignInBySubAsync(path, "  New-Address@Example.com ");

        Assert.True(signedIn);
        Assert.True(verified);
    }

    [Theory]
    [MemberData(nameof(AllSubPaths))]
    public async Task Provider_asserting_the_rows_email_without_verifying_it_proves_nothing(SubPath path)
    {
        var (signedIn, verified) = await SignInBySubAsync(path, RowEmail, providerVerified: false);

        Assert.True(signedIn);
        Assert.False(verified);
    }

    // ── Email sign-up against an existing row ─────────────────────────────────

    public enum RowKind { GoogleLinked, AppleLinked, VerifiedPhone }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class Otp : IOtpService
    {
        public List<(string UserType, string Email)> EmailCodes { get; } = [];
        public Task<string> CreateAndSendEmailOtpAsync(string userType, string email, CancellationToken ct = default)
        {
            EmailCodes.Add((userType, email));
            return Task.FromResult("123456");
        }
        public Task<string> CreateAndSendPhoneOtpAsync(string userType, string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateAndSendPasswordResetAsync(string userType, string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> VerifyAsync(string userType, string provider, string identifier, string code, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Env : IAppEnvironment { public bool IsDevelopment => false; }

    private static Customer CustomerRow(RowKind? kind) => new()
    {
        Email = RowEmail, FullName = "Owner", PasswordHash = OldHash, IsEmailVerified = false, IsActive = true,
        GoogleSub = kind == RowKind.GoogleLinked ? "g-sub" : null,
        AppleSub = kind == RowKind.AppleLinked ? "a-sub" : null,
        IsPhoneVerified = kind == RowKind.VerifiedPhone,
    };

    private static Driver DriverRow(RowKind? kind) => new()
    {
        Email = RowEmail, FullName = "Owner", PasswordHash = OldHash, IsEmailVerified = false,
        GoogleSub = kind == RowKind.GoogleLinked ? "g-sub" : null,
        AppleSub = kind == RowKind.AppleLinked ? "a-sub" : null,
        IsPhoneVerified = kind == RowKind.VerifiedPhone,
    };

    private static CustomerAuthService CustomerService(Customer row, SpyRefreshTokens spy, Otp otp, FakeUnitOfWork uow)
        => new(new FakeCustomerRepo(row), new Hasher(), otp, null!, null!, null!, spy, null!, new Env(), uow);

    private static DriverAuthService DriverService(Driver row, SpyRefreshTokens spy, Otp otp, FakeUnitOfWork uow)
        => new(new FakeDriverRepo(row), new Hasher(), otp, null!, null!, null!, spy, null!, new Env(), null!, null!, uow);

    [Theory]
    [InlineData(RowKind.GoogleLinked)]
    [InlineData(RowKind.AppleLinked)]
    [InlineData(RowKind.VerifiedPhone)]
    public async Task Customer_signup_never_overwrites_a_row_with_an_established_identity(RowKind kind)
    {
        var row = CustomerRow(kind);
        var spy = new SpyRefreshTokens();
        var otp = new Otp();
        var uow = new FakeUnitOfWork();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CustomerService(row, spy, otp, uow).SignUpWithEmailAsync(RowEmail, "attacker-pw", "Attacker"));

        Assert.Equal(AlreadyExists, ex.Message);
        Assert.Equal(OldHash, row.PasswordHash);
        Assert.Equal("Owner", row.FullName);
        Assert.Empty(spy.Calls);
        Assert.Empty(otp.EmailCodes);
        Assert.Equal(0, uow.Saves);
    }

    [Theory]
    [InlineData(RowKind.GoogleLinked)]
    [InlineData(RowKind.AppleLinked)]
    [InlineData(RowKind.VerifiedPhone)]
    public async Task Driver_signup_never_overwrites_a_row_with_an_established_identity(RowKind kind)
    {
        var row = DriverRow(kind);
        var spy = new SpyRefreshTokens();
        var otp = new Otp();
        var uow = new FakeUnitOfWork();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            DriverService(row, spy, otp, uow).SignUpWithEmailAsync(RowEmail, "attacker-pw", "Attacker"));

        Assert.Equal(AlreadyExists, ex.Message);
        Assert.Equal(OldHash, row.PasswordHash);
        Assert.Equal("Owner", row.FullName);
        Assert.Empty(spy.Calls);
        Assert.Empty(otp.EmailCodes);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task Signup_against_a_password_less_social_row_gets_the_same_google_message_as_a_verified_one()
    {
        var row = CustomerRow(RowKind.GoogleLinked);
        row.PasswordHash = null;

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CustomerService(row, new SpyRefreshTokens(), new Otp(), new FakeUnitOfWork())
                .SignUpWithEmailAsync(RowEmail, "pw-12345", "Attacker"));

        Assert.Equal("This email is linked to a Google account. Please continue with Google to sign in.", ex.Message);
        Assert.Null(row.PasswordHash);
    }

    [Fact]
    public async Task Customer_retrying_an_abandoned_signup_overwrites_the_password_and_revokes_sessions()
    {
        var row = CustomerRow(kind: null);
        var spy = new SpyRefreshTokens();
        var otp = new Otp();

        await CustomerService(row, spy, otp, new FakeUnitOfWork()).SignUpWithEmailAsync(RowEmail, "new-pw-123", "Retry");

        Assert.Equal("hash:new-pw-123", row.PasswordHash);
        Assert.Equal("Retry", row.FullName);
        Assert.Contains((row.Id, UserTypes.Customer), spy.RevokedAll);
        Assert.Single(otp.EmailCodes);
    }

    [Fact]
    public async Task Driver_retrying_an_abandoned_signup_overwrites_the_password_and_revokes_sessions()
    {
        var row = DriverRow(kind: null);
        var spy = new SpyRefreshTokens();
        var otp = new Otp();

        await DriverService(row, spy, otp, new FakeUnitOfWork()).SignUpWithEmailAsync(RowEmail, "new-pw-123", "Retry");

        Assert.Equal("hash:new-pw-123", row.PasswordHash);
        Assert.Equal("Retry", row.FullName);
        Assert.Contains((row.Id, "driver"), spy.RevokedAll);
        Assert.Single(otp.EmailCodes);
    }

    // ── Doubles: an account found by its provider sub as well as by email ─────

    private sealed class SubCustomerRepo(Customer? row) : ICustomerRepository
    {
        public Task<Customer?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default)
            => Task.FromResult(row is not null && row.GoogleSub == googleSub ? row : null);
        public Task<Customer?> FindByAppleSubAsync(string appleSub, CancellationToken ct = default)
            => Task.FromResult(row is not null && row.AppleSub == appleSub ? row : null);
        public Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(row is not null && row.Email == email ? row : null);
        public void Update(Customer entity) { }

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> FindByPhoneAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> FindAsync(Expression<Func<Customer, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Customer entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Remove(Customer entity) => throw new NotSupportedException();
    }

    private sealed class SubDriverRepo(Driver? row) : IDriverRepository
    {
        public Task<Driver?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default)
            => Task.FromResult(row is not null && row.GoogleSub == googleSub ? row : null);
        public Task<Driver?> FindByAppleSubAsync(string appleSub, CancellationToken ct = default)
            => Task.FromResult(row is not null && row.AppleSub == appleSub ? row : null);
        public Task<Driver?> FindByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(row is not null && row.Email == email ? row : null);
        public void Update(Driver entity) { }

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> ListByStatusAsync(DriverStatus? status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByPhoneAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByNationalIdNumberAsync(string nationalIdNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByPassportNumberAsync(string passportNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> FindAsync(Expression<Func<Driver, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Driver entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Remove(Driver entity) => throw new NotSupportedException();
    }
}
