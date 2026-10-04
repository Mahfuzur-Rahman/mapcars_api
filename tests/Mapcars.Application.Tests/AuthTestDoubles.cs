using System.Linq.Expressions;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

// Hand-rolled doubles for the auth services (the test project has no mocking
// library). Members a test never reaches throw, so an unexpected call fails loudly
// instead of returning a default that happens to pass.

internal sealed class CaptureEmail : IEmailService
{
    public string ProviderName => "test";
    public string DefaultFromAddress => "no-reply@mapcars.uk";
    public string DefaultFromName => "test";
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string toEmail, string subject, string body, EmailSendOptions? options = null, CancellationToken ct = default)
    {
        Sent.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Saves++;
        return Task.FromResult(0);
    }
}

internal sealed class FakeJwt : IJwtService
{
    public string GenerateToken(Admin admin) => $"admin-jwt:{admin.Id}";
    public string GenerateUserToken(Guid userId, string? identifier, string userType) => $"{userType}-jwt:{userId}";
    public int ExpiryMinutes => 15;
    public int RefreshTokenDays => 30;
}

/// <summary>Records what the auth services ask of the refresh-token service.</summary>
internal sealed class SpyRefreshTokens : IRefreshTokenService
{
    /// <summary>Every call, in order: ("revokeAll" | "issue", userId, userType).</summary>
    public List<(string Call, Guid UserId, string UserType)> Calls { get; } = [];

    public IEnumerable<(Guid UserId, string UserType)> RevokedAll
        => Calls.Where(c => c.Call == "revokeAll").Select(c => (c.UserId, c.UserType));

    public Task<string> IssueAsync(Guid userId, string userType, string? deviceLabel = null, CancellationToken ct = default)
    {
        Calls.Add(("issue", userId, userType));
        return Task.FromResult("refresh-token");
    }

    public Task RevokeAllAsync(Guid userId, string userType, CancellationToken ct = default)
    {
        Calls.Add(("revokeAll", userId, userType));
        return Task.CompletedTask;
    }

    public Task<RefreshResponse> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RevokeAsync(string refreshToken, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class FakeGoogle(GoogleUserInfo info) : IGoogleAuthService
{
    public bool IsConfigured => true;
    public Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
        => Task.FromResult<GoogleUserInfo?>(info);
}

internal sealed class FakeApple(AppleUserInfo info) : IAppleAuthService
{
    public bool IsConfigured => true;
    public Task<AppleUserInfo?> VerifyIdTokenAsync(string idToken, string rawNonce, CancellationToken ct = default)
        => Task.FromResult<AppleUserInfo?>(info);
}

/// <summary>A one-account customer table: found by email, never by a social sub.</summary>
internal sealed class FakeCustomerRepo(Customer? byEmail) : ICustomerRepository
{
    public Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(byEmail is not null && byEmail.Email == email ? byEmail : null);
    public Task<Customer?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
    public Task<Customer?> FindByAppleSubAsync(string appleSub, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
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

/// <summary>A one-account driver table: found by email, never by a social sub.</summary>
internal sealed class FakeDriverRepo(Driver? byEmail) : IDriverRepository
{
    public Task<Driver?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(byEmail is not null && byEmail.Email == email ? byEmail : null);
    public Task<Driver?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default) => Task.FromResult<Driver?>(null);
    public Task<Driver?> FindByAppleSubAsync(string appleSub, CancellationToken ct = default) => Task.FromResult<Driver?>(null);
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

/// <summary>A one-admin table.</summary>
internal sealed class FakeAdminRepo(Admin admin) : IAdminRepository
{
    public List<Admin> Added { get; } = [];

    public Task<Admin?> GetByIdWithRoleAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(id == admin.Id ? admin : Added.FirstOrDefault(a => a.Id == id));
    public Task<List<Menu>> GetMenusForAdminAsync(Guid adminId, int roleId, CancellationToken ct = default)
        => Task.FromResult(new List<Menu>());
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(false);
    public Task AddAsync(Admin newAdmin, CancellationToken ct = default)
    {
        Added.Add(newAdmin);
        return Task.CompletedTask;
    }

    public Task<Admin?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> AnyAdminsExistAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Admin>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Menu>> GetAllMenusAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<int>> GetRoleMenuIdsAsync(int roleId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<AdminMenuPermission>> GetAdminOverridesAsync(Guid adminId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task ReplaceAdminOverridesAsync(Guid adminId, IEnumerable<AdminMenuPermission> overrides, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>An in-memory refresh-token table, keyed by hash as the real one is.</summary>
internal sealed class InMemoryRefreshTokenRepo : IRefreshTokenRepository
{
    public List<RefreshToken> Rows { get; } = [];

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        Rows.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(Rows.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(Guid userId, string userType, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RefreshToken>>(
            Rows.Where(t => t.UserId == userId && t.UserType == userType && t.RevokedAtUtc == null && !t.IsExpired).ToList());

    public Task<int> PurgeExpiredAsync(int olderThanDays, CancellationToken ct = default) => throw new NotSupportedException();
}
