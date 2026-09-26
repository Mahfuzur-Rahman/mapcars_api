using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Mapping;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Auth.Services;

// Single front door for the web app's one sign-in page. Admin, Customer, and
// Driver accounts are still owned by their own feature slices (and the API
// still exposes their dedicated /admin/auth, /auth/customers, /auth/drivers
// endpoints for mobile) — this just tries the email against each table and
// signs in whichever one the password actually matches.
public class UnifiedAuthService(
    IAdminRepository adminRepo,
    ICustomerRepository customerRepo,
    IDriverRepository driverRepo,
    IPasswordHasher hasher,
    IJwtService jwt,
    IRefreshTokenService refreshTokens,
    IGoogleAuthService googleAuth,
    IUnitOfWork uow) : IUnifiedAuthService
{
    public async Task<UnifiedLoginResponse> LoginAsync(UnifiedLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var password = request.Password;

        var admin = await adminRepo.GetByEmailAsync(email, ct);
        if (admin is not null && hasher.Verify(password, admin.PasswordHash))
            return await BuildAdminResponseAsync(admin, await adminRepo.GetMenusForAdminAsync(admin.Id, admin.RoleId, ct), ct);

        var customer = await customerRepo.FindByEmailAsync(email, ct);
        var customerMatches = customer is not null && customer.PasswordHash is not null && hasher.Verify(password, customer.PasswordHash);

        var driver = await driverRepo.FindByEmailAsync(email, ct);
        var driverMatches = driver is not null && driver.PasswordHash is not null && hasher.Verify(password, driver.PasswordHash);

        // The same person can hold a customer account and a driver account under
        // the same email. If both match, don't silently pick one (that used
        // to always mean "customer", since it was checked first) — ask which
        // account they mean, unless they already told us via LoginAs.
        if (customerMatches && driverMatches)
        {
            // LoginAs is accepted under EITHER passenger spelling: a client build
            // that predates the rename still sends the old word, and rejecting it
            // would lock those users out of the unified sign-in entirely.
            if (UserTypes.IsCustomer(request.LoginAs))
                return await BuildUserResponseAsync(customer!, UserTypes.Customer, ct);
            if (UserTypes.IsDriver(request.LoginAs))
                return await BuildUserResponseAsync(driver!, UserTypes.Driver, ct);
            return new UnifiedLoginResponse
            {
                RequiresChoice = true,
                AvailableUserTypes = [UserTypes.Customer, UserTypes.Driver],
            };
        }

        if (customerMatches) return await BuildUserResponseAsync(customer!, UserTypes.Customer, ct);
        if (driverMatches) return await BuildUserResponseAsync(driver!, UserTypes.Driver, ct);

        // Deliberately generic — never reveal which table(s) the email exists
        // in, and never reveal *whose* password was wrong if it happens to
        // exist (with a different password) in more than one table.
        throw new UnauthorizedException("Invalid email or password.");
    }

    public async Task<UnifiedLoginResponse> GoogleLoginAsync(UnifiedGoogleLoginRequest request, CancellationToken ct = default)
    {
        if (!googleAuth.IsConfigured)
            throw new DomainException("Google sign-in isn't available yet. Please use your email or phone number.");

        var info = await googleAuth.VerifyIdTokenAsync(request.IdToken, ct)
            ?? throw new UnauthorizedException("Invalid Google token.");

        var email = info.EmailVerified && !string.IsNullOrWhiteSpace(info.Email)
            ? info.Email.ToLowerInvariant().Trim()
            : null;

        // 1. Look up customer by google_sub or verified email
        var customer = await customerRepo.FindByGoogleSubAsync(info.Sub, ct);
        if (customer is null && email is not null)
        {
            customer = await customerRepo.FindByEmailAsync(email, ct);
            if (customer is not null)
            {
                customer.GoogleSub = info.Sub;
                customerRepo.Update(customer);
                await uow.SaveChangesAsync(ct);
            }
        }

        // 2. Look up driver by google_sub or verified email
        var driver = await driverRepo.FindByGoogleSubAsync(info.Sub, ct);
        if (driver is null && email is not null)
        {
            driver = await driverRepo.FindByEmailAsync(email, ct);
            if (driver is not null)
            {
                driver.GoogleSub = info.Sub;
                driverRepo.Update(driver);
                await uow.SaveChangesAsync(ct);
            }
        }

        // 3. If both accounts exist, handle role choice
        if (customer is not null && driver is not null)
        {
            // LoginAs is accepted under EITHER passenger spelling: a client build
            // that predates the rename still sends the old word, and rejecting it
            // would lock those users out of the unified sign-in entirely.
            if (UserTypes.IsCustomer(request.LoginAs))
                return await BuildUserResponseAsync(customer, UserTypes.Customer, ct);
            if (UserTypes.IsDriver(request.LoginAs))
                return await BuildUserResponseAsync(driver, UserTypes.Driver, ct);
            return new UnifiedLoginResponse
            {
                RequiresChoice = true,
                AvailableUserTypes = [UserTypes.Customer, UserTypes.Driver],
            };
        }

        if (driver is not null)
        {
            if (UserTypes.IsCustomer(request.LoginAs))
            {
                throw new UnauthorizedException("This Google account is registered as a Driver. Please sign in as a driver.");
            }
            return await BuildUserResponseAsync(driver, UserTypes.Driver, ct);
        }

        if (customer is not null)
        {
            if (UserTypes.IsDriver(request.LoginAs))
            {
                throw new UnauthorizedException("This Google account is registered as a Customer. Please sign in as a customer, or register a driver account.");
            }
            return await BuildUserResponseAsync(customer, UserTypes.Customer, ct);
        }

        // 4. Neither exists
        if (!request.SignUp)
        {
            throw new UnauthorizedException(
                "We couldn't find a Mapcars account for that Google account. Please sign up first.");
        }

        if (UserTypes.IsDriver(request.LoginAs))
        {
            var newDriver = new Driver
            {
                GoogleSub = info.Sub,
                Email = email,
                FullName = info.Name,
                IsEmailVerified = email is not null,
                Status = Domain.Enums.DriverStatus.PendingApproval,
            };
            await driverRepo.AddAsync(newDriver, ct);
            await uow.SaveChangesAsync(ct);
            return await BuildUserResponseAsync(newDriver, "driver", ct);
        }
        else
        {
            var newCustomer = new Customer
            {
                GoogleSub = info.Sub,
                Email = email,
                FullName = info.Name,
                IsEmailVerified = email is not null,
            };
            await customerRepo.AddAsync(newCustomer, ct);
            await uow.SaveChangesAsync(ct);
            return await BuildUserResponseAsync(newCustomer, UserTypes.Customer, ct);
        }
    }

    private async Task<UnifiedLoginResponse> BuildAdminResponseAsync(Admin admin, List<Domain.Entities.Menu> menus, CancellationToken ct)
    {
        if (!admin.IsActive)
            throw new UnauthorizedException("This account has been disabled.");

        return new UnifiedLoginResponse
        {
            Token = jwt.GenerateToken(admin),
            ExpiresInMinutes = jwt.ExpiryMinutes,
            RefreshToken = await refreshTokens.IssueAsync(admin.Id, "admin", ct: ct),
            UserType = "admin",
            Admin = admin.ToResponse(),
            Menus = menus.ToMenuTree(),
        };
    }

    private async Task<UnifiedLoginResponse> BuildUserResponseAsync(Customer customer, string userType, CancellationToken ct)
    {
        if (!customer.IsEmailVerified)
            throw new UnauthorizedException("Please verify your email before logging in.");
        if (!customer.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        return new UnifiedLoginResponse
        {
            Token = jwt.GenerateUserToken(customer.Id, customer.Email ?? customer.PhoneNumber, userType),
            ExpiresInMinutes = jwt.ExpiryMinutes,
            RefreshToken = await refreshTokens.IssueAsync(customer.Id, userType, ct: ct),
            UserType = userType,
            UserId = customer.Id,
            FullName = customer.FullName,
            Email = customer.Email,
            Phone = customer.PhoneNumber,
            IsProfileComplete = customer.IsProfileComplete,
            IsEmailVerified = customer.IsEmailVerified,
            IsPhoneVerified = customer.IsPhoneVerified,
        };
    }

    private async Task<UnifiedLoginResponse> BuildUserResponseAsync(Driver driver, string userType, CancellationToken ct)
    {
        if (!driver.IsEmailVerified && string.IsNullOrEmpty(driver.GoogleSub))
            throw new UnauthorizedException("Please verify your email before logging in.");

        return new UnifiedLoginResponse
        {
            Token = jwt.GenerateUserToken(driver.Id, driver.Email ?? driver.PhoneNumber, userType),
            ExpiresInMinutes = jwt.ExpiryMinutes,
            RefreshToken = await refreshTokens.IssueAsync(driver.Id, userType, ct: ct),
            UserType = userType,
            UserId = driver.Id,
            FullName = driver.FullName,
            Email = driver.Email,
            Phone = driver.PhoneNumber,
            IsProfileComplete = driver.IsProfileComplete,
            IsEmailVerified = driver.IsEmailVerified,
            IsPhoneVerified = driver.IsPhoneVerified,
        };
    }
}
