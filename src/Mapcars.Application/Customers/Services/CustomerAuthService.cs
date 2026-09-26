using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth.Interfaces;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Files;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Dtos;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Customers.Services;

public class CustomerAuthService(
    ICustomerRepository repo,
    IPasswordHasher hasher,
    IOtpService otpService,
    IGoogleAuthService googleAuth,
    IJwtService jwt,
    IRefreshTokenService refreshTokens,
    IFileStorageService storage,
    IAppEnvironment env,
    IUnitOfWork uow) : ICustomerAuthService
{
    private const string UserType = UserTypes.Customer;

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

        var customer = await repo.FindByPhoneAsync(normalized, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                PhoneNumber = normalized,
                IsPhoneVerified = true,
                IsActive = true,
            };
            await repo.AddAsync(customer, ct);
        }
        else
        {
            customer.IsPhoneVerified = true;
        }

        await uow.SaveChangesAsync(ct);
        return await BuildResponseAsync(customer, ct);
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
            existing = new Customer
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

        var customer = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Customer", normalized);
        if (customer.IsEmailVerified)
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

        var customer = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new NotFoundException("Customer", normalized);

        customer.IsEmailVerified = true;
        await uow.SaveChangesAsync(ct);
        return await BuildResponseAsync(customer, ct);
    }

    public async Task<AuthResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.ToLowerInvariant().Trim();
        var customer = await repo.FindByEmailAsync(normalized, ct)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!customer.IsEmailVerified)
            throw new UnauthorizedException("Please verify your email before logging in.");

        if (!customer.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        if (customer.PasswordHash is null || !hasher.Verify(password, customer.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return await BuildResponseAsync(customer, ct);
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
        // so trusting it would let anyone who puts a customer's address on a
        // Google account link into (or pre-claim) that customer's account. When
        // unverified we ignore the address entirely: the customer is identified by
        // google_sub alone and can add their email later via the profile.
        var email = info.EmailVerified && !string.IsNullOrWhiteSpace(info.Email)
            ? info.Email.ToLowerInvariant().Trim()
            : null;

        var customer = await repo.FindByGoogleSubAsync(info.Sub, ct);
        if (customer is null)
        {
            // Try to link to an existing (verified-email) account
            customer = email is null ? null : await repo.FindByEmailAsync(email, ct);

            if (customer is not null)
            {
                // Link Google to the existing account — `email` is non-null
                // only when Google vouched for it, so this is safe.
                customer.GoogleSub = info.Sub;
                customer.IsEmailVerified = true;
            }
            else
            {
                // Nothing to sign in to. Coming from the sign-in screen this is
                // "you don't have an account yet" — say so, rather than quietly
                // creating one the customer never asked for.
                if (!signUp)
                    throw new UnauthorizedException(
                        "We couldn't find a Mapcars account for that Google account. Please sign up first.");

                customer = new Customer
                {
                    Email = email,
                    FullName = string.IsNullOrEmpty(info.Name) ? null : info.Name,
                    GoogleSub = info.Sub,
                    IsEmailVerified = email is not null,
                    IsActive = true,
                };
                await repo.AddAsync(customer, ct);
            }

            await uow.SaveChangesAsync(ct);
        }

        if (!customer.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        return await BuildResponseAsync(customer, ct);
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task<CustomerProfileResponse> GetProfileAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);
        return BuildProfileResponse(customer);
    }

    public async Task<CustomerProfileResponse> UpdateProfileAsync(Guid customerId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        customer.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalized = request.Email.ToLowerInvariant().Trim();
            var existing = await repo.FindByEmailAsync(normalized, ct);
            if (existing is not null && existing.Id != customerId)
                throw new DomainException("An account with this email already exists.");
            customer.Email = normalized;
        }

        customer.EmergencyContactName = string.IsNullOrWhiteSpace(request.EmergencyContactName)
            ? customer.EmergencyContactName : request.EmergencyContactName.Trim();
        customer.EmergencyContactPhone = string.IsNullOrWhiteSpace(request.EmergencyContactPhone)
            ? customer.EmergencyContactPhone : request.EmergencyContactPhone.Trim();
        if (request.MarketingConsent.HasValue)
            customer.MarketingConsent = request.MarketingConsent.Value;
        customer.AccessibilityNeeds = string.IsNullOrWhiteSpace(request.AccessibilityNeeds)
            ? customer.AccessibilityNeeds : request.AccessibilityNeeds.Trim();

        await uow.SaveChangesAsync(ct);
        return BuildProfileResponse(customer);
    }

    public async Task ChangePasswordAsync(Guid customerId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        if (customer.PasswordHash is null)
            throw new DomainException("This account has no password set — it was created with Google sign-in.");

        if (!hasher.Verify(request.CurrentPassword, customer.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect.");

        customer.PasswordHash = hasher.Hash(request.NewPassword);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<CustomerProfileResponse> UploadProfilePictureAsync(
        Guid customerId, Stream content, string fileName, string contentType, long fileSize, CancellationToken ct = default)
    {
        // Security gate: profile pictures are images only (no PDF), allowlisted + size-capped.
        FileUploadPolicy.EnsureValidImage(contentType, fileName, fileSize);

        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        customer.ProfilePictureKey = await storage.SaveAsync(content, fileName, contentType, ct);
        customer.ProfilePictureContentType = contentType;

        await uow.SaveChangesAsync(ct);
        return BuildProfileResponse(customer);
    }

    public async Task<(Stream Content, string ContentType)?> GetProfilePictureAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        if (customer.ProfilePictureKey is null) return null;

        var stream = await storage.OpenReadAsync(customer.ProfilePictureKey, ct);
        return stream is null ? null : (stream, customer.ProfilePictureContentType ?? "application/octet-stream");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CustomerProfileResponse BuildProfileResponse(Customer customer) => new()
    {
        CustomerId = customer.Id,
        FullName = customer.FullName,
        Email = customer.Email,
        Phone = customer.PhoneNumber,
        EmergencyContactName = customer.EmergencyContactName,
        EmergencyContactPhone = customer.EmergencyContactPhone,
        MarketingConsent = customer.MarketingConsent,
        AccessibilityNeeds = customer.AccessibilityNeeds,
        HasProfilePicture = customer.ProfilePictureKey is not null,
        IsProfileComplete = customer.IsProfileComplete,
        AverageRating = customer.AverageRating,
        RatingCount = customer.RatingCount,
        CancellationCount = customer.CancellationCount,
        NoShowCount = customer.NoShowCount,
    };

    /// <summary>
    /// Builds the signed-in response, minting both the short-lived access token
    /// and the long-lived refresh token that keeps the customer signed in afterwards.
    /// Async because issuing the refresh token persists it — every caller has
    /// already committed its own changes by this point, so the extra save can't
    /// commit anything half-finished.
    /// </summary>
    private async Task<AuthResponse> BuildResponseAsync(Customer customer, CancellationToken ct) => new()
    {
        Token = jwt.GenerateUserToken(customer.Id, customer.Email ?? customer.PhoneNumber, UserType),
        ExpiresInMinutes = jwt.ExpiryMinutes,
        RefreshToken = await refreshTokens.IssueAsync(customer.Id, UserType, ct: ct),
        UserType = UserType,
        UserId = customer.Id,
        FullName = customer.FullName,
        Email = customer.Email,
        Phone = customer.PhoneNumber,
        IsProfileComplete = customer.IsProfileComplete,
        IsEmailVerified = customer.IsEmailVerified,
        IsPhoneVerified = customer.IsPhoneVerified,
    };

    private static string NormalizePhone(string phone)
        => phone.Trim().Replace(" ", "").Replace("-", "");

    private static string Mask(string s) => s.Length > 4
        ? s[..2] + new string('*', s.Length - 4) + s[^2..]
        : "****";
}
