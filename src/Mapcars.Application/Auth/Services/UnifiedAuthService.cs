using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Mapping;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Auth.Services;

// Single front door for the web app's one sign-in page. Admin, Rider, and
// Driver accounts are still owned by their own feature slices (and the API
// still exposes their dedicated /admin/auth, /auth/riders, /auth/drivers
// endpoints for mobile) — this just tries the email against each table and
// signs in whichever one the password actually matches.
public class UnifiedAuthService(
    IAdminRepository adminRepo,
    IRiderRepository riderRepo,
    IDriverRepository driverRepo,
    IPasswordHasher hasher,
    IJwtService jwt,
    IRefreshTokenService refreshTokens) : IUnifiedAuthService
{
    public async Task<UnifiedLoginResponse> LoginAsync(UnifiedLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.ToLowerInvariant().Trim();
        var password = request.Password;

        var admin = await adminRepo.GetByEmailAsync(email, ct);
        if (admin is not null && hasher.Verify(password, admin.PasswordHash))
            return await BuildAdminResponseAsync(admin, await adminRepo.GetMenusForAdminAsync(admin.Id, admin.RoleId, ct), ct);

        var rider = await riderRepo.FindByEmailAsync(email, ct);
        var riderMatches = rider is not null && rider.PasswordHash is not null && hasher.Verify(password, rider.PasswordHash);

        var driver = await driverRepo.FindByEmailAsync(email, ct);
        var driverMatches = driver is not null && driver.PasswordHash is not null && hasher.Verify(password, driver.PasswordHash);

        // The same person can hold a rider account and a driver account under
        // the same email. If both match, don't silently pick one (that used
        // to always mean "rider", since it was checked first) — ask which
        // account they mean, unless they already told us via LoginAs.
        if (riderMatches && driverMatches)
        {
            return request.LoginAs switch
            {
                "rider" => await BuildUserResponseAsync(rider!, "rider", ct),
                "driver" => await BuildUserResponseAsync(driver!, "driver", ct),
                _ => new UnifiedLoginResponse { RequiresChoice = true, AvailableUserTypes = ["rider", "driver"] },
            };
        }

        if (riderMatches) return await BuildUserResponseAsync(rider!, "rider", ct);
        if (driverMatches) return await BuildUserResponseAsync(driver!, "driver", ct);

        // Deliberately generic — never reveal which table(s) the email exists
        // in, and never reveal *whose* password was wrong if it happens to
        // exist (with a different password) in more than one table.
        throw new UnauthorizedException("Invalid email or password.");
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

    private async Task<UnifiedLoginResponse> BuildUserResponseAsync(Rider rider, string userType, CancellationToken ct)
    {
        if (!rider.IsEmailVerified)
            throw new UnauthorizedException("Please verify your email before logging in.");
        if (!rider.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        return new UnifiedLoginResponse
        {
            Token = jwt.GenerateUserToken(rider.Id, rider.Email ?? rider.PhoneNumber, userType),
            ExpiresInMinutes = jwt.ExpiryMinutes,
            RefreshToken = await refreshTokens.IssueAsync(rider.Id, userType, ct: ct),
            UserType = userType,
            UserId = rider.Id,
            FullName = rider.FullName,
            Email = rider.Email,
            Phone = rider.PhoneNumber,
            IsProfileComplete = rider.IsProfileComplete,
            IsEmailVerified = rider.IsEmailVerified,
            IsPhoneVerified = rider.IsPhoneVerified,
        };
    }

    private async Task<UnifiedLoginResponse> BuildUserResponseAsync(Driver driver, string userType, CancellationToken ct)
    {
        if (!driver.IsEmailVerified)
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
