using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Drivers.Services;

public class DriverAuthService(
    IDriverRepository repo,
    IPasswordHasher hasher,
    IOtpService otpService,
    IGoogleAuthService googleAuth,
    IJwtService jwt,
    IUnitOfWork uow) : IDriverAuthService
{
    private const string UserType = "driver";

    // ── Phone ─────────────────────────────────────────────────────────────────

    public async Task<OtpSentResponse> SendPhoneOtpAsync(string phone, CancellationToken ct = default)
    {
        var normalized = NormalizePhone(phone);
        var devCode = await otpService.CreateAndSendPhoneOtpAsync(UserType, normalized, ct);
        return new OtpSentResponse
        {
            Message = $"Verification code sent to {Mask(normalized)}",
            DevCode = devCode,
        };
    }

    public async Task<AuthResponse> VerifyPhoneOtpAsync(string phone, string code, CancellationToken ct = default)
    {
        var normalized = NormalizePhone(phone);
        if (!await otpService.VerifyAsync(UserType, "phone", normalized, code, ct))
            throw new UnauthorizedException("Invalid or expired code.");

        var driver = await repo.FindByPhoneAsync(normalized, ct);
        if (driver is null)
        {
            driver = new Driver
            {
                PhoneNumber = normalized,
                IsPhoneVerified = true,
                Status = Mapcars.Domain.Enums.DriverStatus.PendingApproval,
            };
            await repo.AddAsync(driver, ct);
        }
        else
        {
            driver.IsPhoneVerified = true;
        }

        await uow.SaveChangesAsync(ct);
        return BuildResponse(driver);
    }

    // ── Email ─────────────────────────────────────────────────────────────────

    public async Task<OtpSentResponse> SignUpWithEmailAsync(string email, string password, string fullName, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();

        var existing = await repo.FindByEmailAsync(normalized, ct);
        if (existing is not null && existing.IsEmailVerified)
            throw new DomainException("An account with this email already exists.");

        if (existing is null)
        {
            existing = new Driver
            {
                Email = normalized,
                FullName = fullName.Trim(),
                PasswordHash = hasher.Hash(password),
                Status = Mapcars.Domain.Enums.DriverStatus.PendingApproval,
            };
            await repo.AddAsync(existing, ct);
        }
        else
        {
            existing.PasswordHash = hasher.Hash(password);
            existing.FullName = fullName.Trim();
        }

        await uow.SaveChangesAsync(ct);
        var devCode = await otpService.CreateAndSendEmailOtpAsync(UserType, normalized, ct);

        return new OtpSentResponse
        {
            Message = $"Verification code sent to {Mask(normalized)}",
            DevCode = devCode,
        };
    }

    public async Task<AuthResponse> VerifyEmailOtpAsync(string email, string code, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();
        if (!await otpService.VerifyAsync(UserType, "email", normalized, code, ct))
            throw new UnauthorizedException("Invalid or expired code.");

        var driver = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Driver", normalized);

        driver.IsEmailVerified = true;
        await uow.SaveChangesAsync(ct);
        return BuildResponse(driver);
    }

    public async Task<AuthResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();
        var driver = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!driver.IsEmailVerified)
            throw new UnauthorizedException("Please verify your email before logging in.");

        if (driver.PasswordHash is null || !hasher.Verify(password, driver.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return BuildResponse(driver);
    }

    // ── Google ────────────────────────────────────────────────────────────────

    public async Task<AuthResponse> SignInWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        var info = await googleAuth.VerifyIdTokenAsync(idToken, ct)
            ?? throw new UnauthorizedException("Invalid Google token.");

        var driver = await repo.FindByGoogleSubAsync(info.Sub, ct);
        if (driver is null)
        {
            driver = string.IsNullOrEmpty(info.Email)
                ? null
                : await repo.FindByEmailAsync(info.Email.ToLowerInvariant(), ct);

            if (driver is not null)
            {
                driver.GoogleSub = info.Sub;
                if (info.EmailVerified) driver.IsEmailVerified = true;
            }
            else
            {
                driver = new Driver
                {
                    Email = string.IsNullOrEmpty(info.Email) ? null : info.Email.ToLowerInvariant(),
                    FullName = string.IsNullOrEmpty(info.Name) ? null : info.Name,
                    GoogleSub = info.Sub,
                    IsEmailVerified = info.EmailVerified,
                    Status = Mapcars.Domain.Enums.DriverStatus.PendingApproval,
                };
                await repo.AddAsync(driver, ct);
            }

            await uow.SaveChangesAsync(ct);
        }

        return BuildResponse(driver);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AuthResponse BuildResponse(Driver driver) => new()
    {
        Token = jwt.GenerateUserToken(driver.Id, driver.Email ?? driver.PhoneNumber, UserType),
        ExpiresInMinutes = jwt.ExpiryMinutes,
        UserType = UserType,
        UserId = driver.Id,
        FullName = driver.FullName,
        Email = driver.Email,
        Phone = driver.PhoneNumber,
        IsProfileComplete = driver.IsProfileComplete,
        IsEmailVerified = driver.IsEmailVerified,
        IsPhoneVerified = driver.IsPhoneVerified,
    };

    private static string NormalizePhone(string phone)
        => phone.Trim().Replace(" ", "").Replace("-", "");

    private static string Mask(string s) => s.Length > 4
        ? s[..2] + new string('*', s.Length - 4) + s[^2..]
        : "****";
}
