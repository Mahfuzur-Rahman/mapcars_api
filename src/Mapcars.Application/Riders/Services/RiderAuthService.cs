using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Riders.Services;

public class RiderAuthService(
    IRiderRepository repo,
    IPasswordHasher hasher,
    IOtpService otpService,
    IGoogleAuthService googleAuth,
    IJwtService jwt,
    IUnitOfWork uow) : IRiderAuthService
{
    private const string UserType = "rider";

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

        var rider = await repo.FindByPhoneAsync(normalized, ct);
        if (rider is null)
        {
            rider = new Rider
            {
                PhoneNumber = normalized,
                IsPhoneVerified = true,
                IsActive = true,
            };
            await repo.AddAsync(rider, ct);
        }
        else
        {
            rider.IsPhoneVerified = true;
        }

        await uow.SaveChangesAsync(ct);
        return BuildResponse(rider);
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
            existing = new Rider
            {
                Email = normalized,
                FullName = fullName.Trim(),
                PasswordHash = hasher.Hash(password),
                IsActive = true,
            };
            await repo.AddAsync(existing, ct);
        }
        else
        {
            // Re-registration attempt — update password/name
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

    public async Task<OtpSentResponse> ResendEmailOtpAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();

        var rider = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Rider", normalized);
        if (rider.IsEmailVerified)
            throw new DomainException("This email is already verified. Please log in.");

        // Issuing a new code invalidates the previous one (see OtpService).
        var devCode = await otpService.CreateAndSendEmailOtpAsync(UserType, normalized, ct);
        return new OtpSentResponse
        {
            Message = $"A new verification code was sent to {Mask(normalized)}",
            DevCode = devCode,
        };
    }

    public async Task<AuthResponse> VerifyEmailOtpAsync(string email, string code, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();
        if (!await otpService.VerifyAsync(UserType, "email", normalized, code, ct))
            throw new UnauthorizedException("Invalid or expired code.");

        var rider = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Rider", normalized);

        rider.IsEmailVerified = true;
        await uow.SaveChangesAsync(ct);
        return BuildResponse(rider);
    }

    public async Task<AuthResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();
        var rider = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!rider.IsEmailVerified)
            throw new UnauthorizedException("Please verify your email before logging in.");

        if (!rider.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        if (rider.PasswordHash is null || !hasher.Verify(password, rider.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return BuildResponse(rider);
    }

    // ── Google ────────────────────────────────────────────────────────────────

    public async Task<AuthResponse> SignInWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        var info = await googleAuth.VerifyIdTokenAsync(idToken, ct)
            ?? throw new UnauthorizedException("Invalid Google token.");

        var rider = await repo.FindByGoogleSubAsync(info.Sub, ct);
        if (rider is null)
        {
            // Try to link to an existing email account
            rider = string.IsNullOrEmpty(info.Email)
                ? null
                : await repo.FindByEmailAsync(info.Email.ToLowerInvariant(), ct);

            if (rider is not null)
            {
                // Link Google to the existing account
                rider.GoogleSub = info.Sub;
                if (info.EmailVerified) rider.IsEmailVerified = true;
            }
            else
            {
                rider = new Rider
                {
                    Email = string.IsNullOrEmpty(info.Email) ? null : info.Email.ToLowerInvariant(),
                    FullName = string.IsNullOrEmpty(info.Name) ? null : info.Name,
                    GoogleSub = info.Sub,
                    IsEmailVerified = info.EmailVerified,
                    IsActive = true,
                };
                await repo.AddAsync(rider, ct);
            }

            await uow.SaveChangesAsync(ct);
        }

        if (!rider.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        return BuildResponse(rider);
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task<AuthResponse> UpdateProfileAsync(Guid riderId, string fullName, string? email, CancellationToken ct = default)
    {
        var rider = await repo.GetByIdAsync(riderId, ct)
            ?? throw new NotFoundException("Rider", riderId);

        rider.FullName = fullName.Trim();

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.ToLowerInvariant().Trim();
            var existing = await repo.FindByEmailAsync(normalized, ct);
            if (existing is not null && existing.Id != riderId)
                throw new DomainException("An account with this email already exists.");
            rider.Email = normalized;
        }

        await uow.SaveChangesAsync(ct);
        return BuildResponse(rider);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AuthResponse BuildResponse(Rider rider) => new()
    {
        Token = jwt.GenerateUserToken(rider.Id, rider.Email ?? rider.PhoneNumber, UserType),
        ExpiresInMinutes = jwt.ExpiryMinutes,
        UserType = UserType,
        UserId = rider.Id,
        FullName = rider.FullName,
        Email = rider.Email,
        Phone = rider.PhoneNumber,
        IsProfileComplete = rider.IsProfileComplete,
        IsEmailVerified = rider.IsEmailVerified,
        IsPhoneVerified = rider.IsPhoneVerified,
    };

    private static string NormalizePhone(string phone)
        => phone.Trim().Replace(" ", "").Replace("-", "");

    private static string Mask(string s) => s.Length > 4
        ? s[..2] + new string('*', s.Length - 4) + s[^2..]
        : "****";
}
