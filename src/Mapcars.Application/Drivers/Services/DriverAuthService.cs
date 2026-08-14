using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Files;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Dtos;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Drivers.Services;

public class DriverAuthService(
    IDriverRepository repo,
    IPasswordHasher hasher,
    IOtpService otpService,
    IGoogleAuthService googleAuth,
    IJwtService jwt,
    IRefreshTokenService refreshTokens,
    IFileStorageService storage,
    IAppEnvironment env,
    IDriverLocationStore locations,
    IUnitOfWork uow) : IDriverAuthService
{
    private const string UserType = "driver";

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

        var driver = await repo.FindByPhoneAsync(normalized, ct);
        if (driver is null)
        {
            // Every new driver starts unapproved — signing up never grants the
            // right to work. Only an admin's explicit decision (AdminDriverReview
            // → SetDriverStatus) moves a driver to Approved.
            driver = new Driver
            {
                PhoneNumber = normalized,
                IsPhoneVerified = true,
                Status = DriverStatus.PendingApproval,
            };
            await repo.AddAsync(driver, ct);
        }
        else
        {
            driver.IsPhoneVerified = true;
        }

        await uow.SaveChangesAsync(ct);
        return await BuildResponseAsync(driver, ct);
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
            // Unapproved until an admin says otherwise — see VerifyPhoneOtpAsync.
            existing = new Driver
            {
                Email = normalized,
                FullName = fullName.Trim(),
                PasswordHash = hasher.Hash(password),
                Status = DriverStatus.PendingApproval,
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

        return OtpSent($"Verification code sent to {Mask(normalized)}", devCode);
    }

    public async Task<OtpSentResponse> ResendEmailOtpAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();

        var driver = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Driver", normalized);
        if (driver.IsEmailVerified)
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

        var driver = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Driver", normalized);

        driver.IsEmailVerified = true;
        await uow.SaveChangesAsync(ct);
        return await BuildResponseAsync(driver, ct);
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

        return await BuildResponseAsync(driver, ct);
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
        // so trusting it would let anyone who puts a driver's address on a
        // Google account link into (or pre-claim) that driver's account. When
        // unverified we ignore the address entirely: the driver is identified
        // by google_sub alone and can add their email later via the profile.
        var email = info.EmailVerified && !string.IsNullOrWhiteSpace(info.Email)
            ? info.Email.ToLowerInvariant().Trim()
            : null;

        var driver = await repo.FindByGoogleSubAsync(info.Sub, ct);
        if (driver is null)
        {
            driver = email is null ? null : await repo.FindByEmailAsync(email, ct);

            if (driver is not null)
            {
                // `email` is non-null only when Google vouched for it.
                driver.GoogleSub = info.Sub;
                driver.IsEmailVerified = true;
            }
            else
            {
                // Nothing to sign in to — see the rider service: an unknown
                // Google account on the sign-in screen is told to sign up.
                if (!signUp)
                    throw new UnauthorizedException(
                        "We couldn't find a Mapcars driver account for that Google account. Please sign up first.");

                driver = new Driver
                {
                    Email = email,
                    FullName = string.IsNullOrEmpty(info.Name) ? null : info.Name,
                    GoogleSub = info.Sub,
                    IsEmailVerified = email is not null,
                    Status = DriverStatus.PendingApproval,
                };
                await repo.AddAsync(driver, ct);
            }

            await uow.SaveChangesAsync(ct);
        }

        return await BuildResponseAsync(driver, ct);
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task<DriverProfileResponse> GetProfileAsync(Guid driverId, CancellationToken ct = default)
    {
        var driver = await repo.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);
        return BuildProfileResponse(driver);
    }

    public async Task<DriverProfileResponse> UpdateProfileAsync(Guid driverId, UpdateDriverProfileRequest req, CancellationToken ct = default)
    {
        var driver = await repo.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        var nationalId = req.NationalIdNumber.Trim();
        var existingById = await repo.FindByNationalIdNumberAsync(nationalId, ct);
        if (existingById is not null && existingById.Id != driverId)
            throw new DomainException("A driver with this national ID number already exists.");

        if (!string.IsNullOrWhiteSpace(req.PassportNumber))
        {
            var passportNumber = req.PassportNumber.Trim();
            var existingByPassport = await repo.FindByPassportNumberAsync(passportNumber, ct);
            if (existingByPassport is not null && existingByPassport.Id != driverId)
                throw new DomainException("A driver with this passport number already exists.");
            driver.PassportNumber = passportNumber;
        }

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var normalized = req.Email.ToLowerInvariant().Trim();
            var existingByEmail = await repo.FindByEmailAsync(normalized, ct);
            if (existingByEmail is not null && existingByEmail.Id != driverId)
                throw new DomainException("An account with this email already exists.");
            driver.Email = normalized;
        }

        driver.FirstName = req.FirstName.Trim();
        driver.LastName = string.IsNullOrWhiteSpace(req.LastName) ? null : req.LastName.Trim();
        driver.FullName = string.IsNullOrEmpty(driver.LastName)
            ? driver.FirstName
            : $"{driver.FirstName} {driver.LastName}";
        driver.DateOfBirth = req.DateOfBirth;
        driver.Address = req.Address?.Trim();
        driver.NationalIdNumber = nationalId;
        driver.DrivingLicenceNumber = string.IsNullOrWhiteSpace(req.DrivingLicenceNumber)
            ? driver.DrivingLicenceNumber : req.DrivingLicenceNumber.Trim();
        driver.EmergencyContactName = string.IsNullOrWhiteSpace(req.EmergencyContactName)
            ? driver.EmergencyContactName : req.EmergencyContactName.Trim();
        driver.EmergencyContactPhone = string.IsNullOrWhiteSpace(req.EmergencyContactPhone)
            ? driver.EmergencyContactPhone : req.EmergencyContactPhone.Trim();
        if (req.MarketingConsent.HasValue)
            driver.MarketingConsent = req.MarketingConsent.Value;

        await uow.SaveChangesAsync(ct);
        return BuildProfileResponse(driver);
    }

    public async Task ChangePasswordAsync(Guid driverId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var driver = await repo.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        if (driver.PasswordHash is null)
            throw new DomainException("This account has no password set — it was created with Google sign-in.");

        if (!hasher.Verify(request.CurrentPassword, driver.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect.");

        driver.PasswordHash = hasher.Hash(request.NewPassword);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<DriverProfileResponse> UploadProfilePictureAsync(
        Guid driverId, Stream content, string fileName, string contentType, long fileSize, CancellationToken ct = default)
    {
        // Security gate: profile pictures are images only (no PDF), allowlisted + size-capped.
        FileUploadPolicy.EnsureValidImage(contentType, fileName, fileSize);

        var driver = await repo.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        driver.ProfilePictureKey = await storage.SaveAsync(content, fileName, contentType, ct);
        driver.ProfilePictureContentType = contentType;

        await uow.SaveChangesAsync(ct);
        return BuildProfileResponse(driver);
    }

    public async Task<(Stream Content, string ContentType)?> GetProfilePictureAsync(Guid driverId, CancellationToken ct = default)
    {
        var driver = await repo.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        if (driver.ProfilePictureKey is null) return null;

        var stream = await storage.OpenReadAsync(driver.ProfilePictureKey, ct);
        return stream is null ? null : (stream, driver.ProfilePictureContentType ?? "application/octet-stream");
    }

    public async Task<DriverProfileResponse> SetAvailabilityAsync(Guid driverId, bool isOnline, CancellationToken ct = default)
    {
        var driver = await repo.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        // Going online is gated on admin approval — no environment, demo account
        // or client flag can bypass it. Going *offline* is always allowed (a
        // suspended driver must still be able to take themselves off the road).
        if (isOnline && !DriverApproval.CanWork(driver))
            throw new DomainException(DriverApproval.BlockedMessage(driver.Status));

        driver.IsOnline = isOnline;
        if (isOnline) driver.LastOnlineAtUtc = DateTime.UtcNow;

        await uow.SaveChangesAsync(ct);

        // Leaving the road also means leaving the live GEO pool, so no rider or
        // dispatch broadcast can still see this driver as available.
        if (!isOnline) await locations.RemoveAsync(driverId, ct);

        return BuildProfileResponse(driver);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DriverProfileResponse BuildProfileResponse(Driver driver) => new()
    {
        DriverId = driver.Id,
        FirstName = driver.FirstName,
        LastName = driver.LastName,
        FullName = driver.FullName,
        Email = driver.Email,
        Phone = driver.PhoneNumber,
        DateOfBirth = driver.DateOfBirth,
        Address = driver.Address,
        NationalIdNumber = driver.NationalIdNumber,
        DrivingLicenceNumber = driver.DrivingLicenceNumber,
        PassportNumber = driver.PassportNumber,
        EmergencyContactName = driver.EmergencyContactName,
        EmergencyContactPhone = driver.EmergencyContactPhone,
        MarketingConsent = driver.MarketingConsent,
        HasProfilePicture = driver.ProfilePictureKey is not null,
        IsProfileComplete = driver.IsProfileComplete,
        Status = driver.Status.ToString(),
        IsOnline = driver.IsOnline,
        LastOnlineAtUtc = driver.LastOnlineAtUtc,
        AverageRating = driver.AverageRating,
        RatingCount = driver.RatingCount,
        CancellationCount = driver.CancellationCount,
        NoShowCount = driver.NoShowCount,
        CreatedAtUtc = driver.CreatedAtUtc,
    };

    /// <summary>
    /// Builds the signed-in response, minting both the short-lived access token
    /// and the long-lived refresh token that keeps the driver signed in afterwards.
    /// Async because issuing the refresh token persists it — every caller has
    /// already committed its own changes by this point, so the extra save can't
    /// commit anything half-finished.
    /// </summary>
    private async Task<AuthResponse> BuildResponseAsync(Driver driver, CancellationToken ct) => new()
    {
        Token = jwt.GenerateUserToken(driver.Id, driver.Email ?? driver.PhoneNumber, UserType),
        ExpiresInMinutes = jwt.ExpiryMinutes,
        RefreshToken = await refreshTokens.IssueAsync(driver.Id, UserType, ct: ct),
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
