using System.Linq.Expressions;
using Mapcars.Application.Customers.Dtos;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Customers.Services;
using Mapcars.Application.Drivers.Dtos;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Drivers.Services;
using Mapcars.Application.Settings.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// Email is the identity a Google/Apple sign-in links to, and a verified flag is
/// what lets <c>SocialAccountLink.ClaimAsync</c> leave an account's password alone.
/// So the flag must describe the address now on the row: if a profile edit swapped
/// the email but kept the flag, an attacker could point their verified account at a
/// victim's address and the victim's own social sign-in would link into it with the
/// takeover protection skipped.
/// </summary>
public class ProfileEmailChangeTests
{
    private const string Current = "owner@example.com";
    private const string Changed = "victim@example.com";

    // ── Customer ──────────────────────────────────────────────────────────────

    private static async Task<Customer> UpdateCustomerAsync(string? requestEmail, Customer? other = null)
    {
        var customer = new Customer { Email = Current, FullName = "Owner", IsEmailVerified = true, IsActive = true };
        var service = new CustomerAuthService(
            new ProfileCustomerRepo(customer, other), null!, null!, null!, null!, null!, null!, null!, null!, new FakeUnitOfWork());
        await service.UpdateProfileAsync(customer.Id, new UpdateProfileRequest("Owner", requestEmail));
        return customer;
    }

    [Fact]
    public async Task Customer_changing_email_resets_verification()
    {
        var customer = await UpdateCustomerAsync(Changed);

        Assert.Equal(Changed, customer.Email);
        Assert.False(customer.IsEmailVerified);
    }

    [Theory]
    [InlineData(Current)]
    [InlineData("  Owner@Example.com ")] // case/whitespace only: the same mailbox
    [InlineData(null)]                    // email left out of the request entirely
    [InlineData("")]
    public async Task Customer_keeping_the_same_email_stays_verified(string? requestEmail)
    {
        var customer = await UpdateCustomerAsync(requestEmail);

        Assert.Equal(Current, customer.Email);
        Assert.True(customer.IsEmailVerified);
    }

    [Fact]
    public async Task Customer_changing_email_to_one_already_taken_is_rejected_and_stays_verified()
    {
        var taken = new Customer { Email = Changed };
        var customer = new Customer { Email = Current, IsEmailVerified = true };
        var service = new CustomerAuthService(
            new ProfileCustomerRepo(customer, taken), null!, null!, null!, null!, null!, null!, null!, null!, new FakeUnitOfWork());

        await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateProfileAsync(customer.Id, new UpdateProfileRequest("Owner", Changed)));

        Assert.Equal(Current, customer.Email);
        Assert.True(customer.IsEmailVerified);
    }

    // ── Driver ────────────────────────────────────────────────────────────────

    private static async Task<Driver> UpdateDriverAsync(string? requestEmail)
    {
        var driver = new Driver { Email = Current, IsEmailVerified = true };
        var service = new DriverAuthService(
            new ProfileDriverRepo(driver, null), null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new DefaultSettings(), new FakeUnitOfWork());
        await service.UpdateProfileAsync(driver.Id, new UpdateDriverProfileRequest(
            FirstName: "Owner", LastName: null, Email: requestEmail, DateOfBirth: null, Address: null,
            NationalIdNumber: "NI123"));
        return driver;
    }

    [Fact]
    public async Task Driver_changing_email_resets_verification()
    {
        var driver = await UpdateDriverAsync(Changed);

        Assert.Equal(Changed, driver.Email);
        Assert.False(driver.IsEmailVerified);
    }

    [Theory]
    [InlineData(Current)]
    [InlineData("  Owner@Example.com ")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Driver_keeping_the_same_email_stays_verified(string? requestEmail)
    {
        var driver = await UpdateDriverAsync(requestEmail);

        Assert.Equal(Current, driver.Email);
        Assert.True(driver.IsEmailVerified);
    }

    [Fact]
    public async Task Driver_changing_email_to_one_already_taken_is_rejected_and_stays_verified()
    {
        var taken = new Driver { Email = Changed };
        var driver = new Driver { Email = Current, IsEmailVerified = true };
        var service = new DriverAuthService(
            new ProfileDriverRepo(driver, taken), null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new DefaultSettings(), new FakeUnitOfWork());

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateProfileAsync(driver.Id, new UpdateDriverProfileRequest(
            FirstName: "Owner", LastName: null, Email: Changed, DateOfBirth: null, Address: null,
            NationalIdNumber: "NI123")));

        Assert.Equal(Current, driver.Email);
        Assert.True(driver.IsEmailVerified);
    }

    // ── Doubles (only what UpdateProfileAsync reaches) ────────────────────────

    /// <summary>Looks up by id (the caller) and by email (the caller, or one other account).</summary>
    private sealed class ProfileCustomerRepo(Customer self, Customer? other) : ICustomerRepository
    {
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Customer?>(id == self.Id ? self : null);
        public Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(new[] { self, other }.FirstOrDefault(c => c is not null && c.Email == email));
        public void Update(Customer entity) { }

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> FindByPhoneAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Customer?> FindByAppleSubAsync(string appleSub, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Customer>> FindAsync(Expression<Func<Customer, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Customer entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Remove(Customer entity) => throw new NotSupportedException();
    }

    private sealed class ProfileDriverRepo(Driver self, Driver? other) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Driver?>(id == self.Id ? self : null);
        public Task<Driver?> FindByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(new[] { self, other }.FirstOrDefault(d => d is not null && d.Email == email));
        public Task<Driver?> FindByNationalIdNumberAsync(string nationalIdNumber, CancellationToken ct = default)
            => Task.FromResult<Driver?>(null);
        public void Update(Driver entity) { }

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> ListByStatusAsync(DriverStatus? status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByPhoneAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByPassportNumberAsync(string passportNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Driver?> FindByAppleSubAsync(string appleSub, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> FindAsync(Expression<Func<Driver, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Driver entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Remove(Driver entity) => throw new NotSupportedException();
    }

    /// <summary>The driver profile response reads payment settings; defaults are all it needs.</summary>
    private sealed class DefaultSettings : ISettingsStore
    {
        public Task<T> GetAsync<T>(string key, CancellationToken ct = default) where T : class, new()
            => Task.FromResult(new T());
        public Task<T> SetAsync<T>(string key, T value, Guid? updatedByAdminId = null, CancellationToken ct = default)
            where T : class, new() => throw new NotSupportedException();
    }
}
