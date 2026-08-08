using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Riders.Dtos;
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
    IAppEnvironment env,
    IUnitOfWork uow) : IRiderAuthService
{
    private const string UserType = "rider";

    // The OTP is only ever revealed to the caller in local Development (a
    // convenience so devs can log in without a live SMS/email provider). In
    // Production it is null — never ship the code that authenticates the user.
    private OtpSentResponse OtpSent(string message, string code) => new()
    {
        Message = message,
        DevCode = env.IsDevelopment ? code : null,
    };

    // ── Phone ─────────────────────────────────────────────────────────────────

    public async Task<OtpSentResponse> SendPhoneOtpAsync(string phone, CancellationToken ct = default)
    {
        var normalized = NormalizePhone(phone);
        var devCode = await otpService.CreateAndSendPhoneOtpAsync(UserType, normalized, ct);
        return OtpSent($"Verification code sent to {Mask(normalized)}", devCode);
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
            throw new DomainException(existing.PasswordHash is null
                // Google-only account — never had a password to overwrite.
                ? "This email is linked to a Google account. Please continue with Google to sign in."
                : "An account with this email already exists. Please log in instead.");

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

        return OtpSent($"Verification code sent to {Mask(normalized)}", devCode);
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
        return OtpSent($"A new verification code was sent to {Mask(normalized)}", devCode);
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

    public async Task<AuthResponse> SignInWithGoogleAsync(string idToken, bool signUp = false, CancellationToken ct = default)
    {
        if (!googleAuth.IsConfigured)
            throw new DomainException("Google sign-in isn't available yet. Please use your email or phone number.");

        var info = await googleAuth.VerifyIdTokenAsync(idToken, ct)
            ?? throw new UnauthorizedException("Invalid Google token.");

        // Only an address Google says it *verified* may identify an account.
        // An unverified one is just a string the Google account holder typed,
        // so trusting it would let anyone who puts a rider's address on a
        // Google account link into (or pre-claim) that rider's account. When
        // unverified we ignore the address entirely: the rider is identified by
        // google_sub alone and can add their email later via the profile.
        var email = info.EmailVerified && !string.IsNullOrWhiteSpace(info.Email)
            ? info.Email.ToLowerInvariant().Trim()
            : null;

        var rider = await repo.FindByGoogleSubAsync(info.Sub, ct);
        if (rider is null)
        {
            // Try to link to an existing (verified-email) account
            rider = email is null ? null : await repo.FindByEmailAsync(email, ct);

            if (rider is not null)
            {
                // Link Google to the existing account — `email` is non-null
                // only when Google vouched for it, so this is safe.
                rider.GoogleSub = info.Sub;
                rider.IsEmailVerified = true;
            }
            else
            {
                // Nothing to sign in to. Coming from the sign-in screen this is
                // "you don't have an account yet" — say so, rather than quietly
                // creating one the rider never asked for.
                if (!signUp)
                    throw new UnauthorizedException(
                        "We couldn't find a Mapcars account for that Google account. Please sign up first.");

                rider = new Rider
                {
                    Email = email,
                    FullName = string.IsNullOrEmpty(info.Name) ? null : info.Name,
                    GoogleSub = info.Sub,
                    IsEmailVerified = email is not null,
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

    public async Task<RiderProfileResponse> GetProfileAsync(Guid riderId, CancellationToken ct = default)
    {
        var rider = await repo.GetByIdAsync(riderId, ct)
            ?? throw new NotFoundException("Rider", riderId);
        return BuildProfileResponse(rider);
    }

    public async Task<RiderProfileResponse> UpdateProfileAsync(Guid riderId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var rider = await repo.GetByIdAsync(riderId, ct)
            ?? throw new NotFoundException("Rider", riderId);

        rider.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalized = request.Email.ToLowerInvariant().Trim();
            var existing = await repo.FindByEmailAsync(normalized, ct);
            if (existing is not null && existing.Id != riderId)
                throw new DomainException("An account with this email already exists.");
            rider.Email = normalized;
        }

        rider.EmergencyContactName = string.IsNullOrWhiteSpace(request.EmergencyContactName)
            ? rider.EmergencyContactName : request.EmergencyContactName.Trim();
        rider.EmergencyContactPhone = string.IsNullOrWhiteSpace(request.EmergencyContactPhone)
            ? rider.EmergencyContactPhone : request.EmergencyContactPhone.Trim();
        if (request.MarketingConsent.HasValue)
            rider.MarketingConsent = request.MarketingConsent.Value;
        rider.AccessibilityNeeds = string.IsNullOrWhiteSpace(request.AccessibilityNeeds)
            ? rider.AccessibilityNeeds : request.AccessibilityNeeds.Trim();

        await uow.SaveChangesAsync(ct);
        return BuildProfileResponse(rider);
    }

    public async Task ChangePasswordAsync(Guid riderId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var rider = await repo.GetByIdAsync(riderId, ct)
            ?? throw new NotFoundException("Rider", riderId);

        if (rider.PasswordHash is null)
            throw new DomainException("This account has no password set — it was created with Google sign-in.");

        if (!hasher.Verify(request.CurrentPassword, rider.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect.");

        rider.PasswordHash = hasher.Hash(request.NewPassword);
        await uow.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RiderProfileResponse BuildProfileResponse(Rider rider) => new()
    {
        RiderId = rider.Id,
        FullName = rider.FullName,
        Email = rider.Email,
        Phone = rider.PhoneNumber,
        EmergencyContactName = rider.EmergencyContactName,
        EmergencyContactPhone = rider.EmergencyContactPhone,
        MarketingConsent = rider.MarketingConsent,
        AccessibilityNeeds = rider.AccessibilityNeeds,
        IsProfileComplete = rider.IsProfileComplete,
        AverageRating = rider.AverageRating,
        RatingCount = rider.RatingCount,
        CancellationCount = rider.CancellationCount,
        NoShowCount = rider.NoShowCount,
    };

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
