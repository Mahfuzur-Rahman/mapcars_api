using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Auth;
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
    IAppleAuthService appleAuth,
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
        // An unverified row is normally an abandoned sign-up the owner of the
        // address may retry. But one tied to a Google/Apple identity or a verified
        // phone is a live account whose email was merely reset by a profile edit —
        // overwriting its password would let anyone who knows the address plant
        // one, which the owner is then told to "verify" into place. Same answer as
        // for a verified account, so nothing new is revealed.
        if (existing is not null && (existing.IsEmailVerified || SocialAccountLink.HasEstablishedIdentity(existing)))
            throw new DomainException(existing.PasswordHash is null && existing.GoogleSub is not null
                // Google-only account — never had a password to overwrite. Only a row that
                // really has a GoogleSub gets this wording; Apple-only / phone-only rows get
                // the generic message so no other sign-in method is revealed.
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
            // Re-registration attempt — update password/name. Whoever set the old
            // password may not be the one retrying, so their sessions go too.
            existing.PasswordHash = hasher.Hash(password);
            existing.FullName = fullName.Trim();
            await refreshTokens.RevokeAllAsync(existing.Id, UserType, ct);
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
                // only when Google vouched for it. Claiming first strips any
                // password/sessions an unverified pre-registration left behind.
                await SocialAccountLink.ClaimAsync(customer, refreshTokens, ct);
                customer.GoogleSub = info.Sub;
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
        // Found by sub: signs in whatever its email flag says (a profile edit may
        // have reset it). The provider vouching for the row's own address restores it.
        else if (SocialAccountLink.ConfirmEmail(customer, email))
        {
            await uow.SaveChangesAsync(ct);
        }

        if (!customer.IsActive)
            throw new UnauthorizedException("Your account has been disabled.");

        return await BuildResponseAsync(customer, ct);
    }

    // ── Apple ─────────────────────────────────────────────────────────────────

    public async Task<AuthResponse> SignInWithAppleAsync(AppleAuthRequest request, CancellationToken ct = default)
    {
        if (!appleAuth.IsConfigured)
            throw new DomainException("Sign in with Apple isn't available yet. Please use your email or phone number.");

        var info = await appleAuth.VerifyIdTokenAsync(request.IdToken, request.Nonce, ct)
            ?? throw new UnauthorizedException("Invalid Apple sign-in.");

        // Same rule as Google: only a verified address may link to an existing
        // account. Apple only sends the email on the first authorisation, so on
        // every later sign-in the account is found by apple_sub alone.
        var email = info.EmailVerified && !string.IsNullOrWhiteSpace(info.Email)
            ? info.Email.ToLowerInvariant().Trim()
            : null;

        var customer = await repo.FindByAppleSubAsync(info.Sub, ct);
        if (customer is null)
        {
            customer = email is null ? null : await repo.FindByEmailAsync(email, ct);

            if (customer is not null)
            {
                await SocialAccountLink.ClaimAsync(customer, refreshTokens, ct);
                customer.AppleSub = info.Sub;
            }
            else
            {
                if (!request.SignUp)
                    throw new UnauthorizedException(
                        "We couldn't find a Mapcars account for that Apple ID. Please sign up first.");

                customer = new Customer
                {
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim(),
                    AppleSub = info.Sub,
                    IsEmailVerified = email is not null,
                    IsActive = true,
                };
                await repo.AddAsync(customer, ct);
            }

            await uow.SaveChangesAsync(ct);
        }
        // Found by sub: signs in whatever its email flag says (a profile edit may
        // have reset it). The provider vouching for the row's own address restores it.
        else if (SocialAccountLink.ConfirmEmail(customer, email))
        {
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

            // A verified flag vouches for the address it was earned on. Carrying it
            // over to a new address would let an attacker aim a verified account at
            // a victim's email, so the victim's later Google/Apple sign-in would link
            // into it with SocialAccountLink's takeover protection skipped. Case is
            // ignored because lookups lower-case, so "A@x.com" is the same mailbox.
            if (!string.Equals(customer.Email, normalized, StringComparison.OrdinalIgnoreCase))
                customer.IsEmailVerified = false;
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

    // ── Profile email verification ────────────────────────────────────────────
    // A phone sign-up adds an email on the profile, and it starts unverified (see
    // UpdateProfileAsync). Trip emails only go to a verified address, so a
    // mistyped one never receives somebody's pickup and drop-off. The code is
    // checked against the address already on the profile — the request names none.
    // Same "email" code as sign-up: it is for this address, which is the point.

    public async Task<OtpSentResponse> SendProfileEmailCodeAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);
        if (string.IsNullOrWhiteSpace(customer.Email))
            throw new DomainException("Add an email address to your profile first.");
        if (customer.IsEmailVerified)
            throw new DomainException("Your email address is already verified.");

        var devCode = await otpService.CreateAndSendEmailOtpAsync(UserType, customer.Email, ct);
        return OtpSent($"Verification code sent to {Mask(customer.Email)}", devCode);
    }

    public async Task<CustomerProfileResponse> VerifyProfileEmailAsync(Guid customerId, string code, CancellationToken ct = default)
    {
        var customer = await repo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);
        if (string.IsNullOrWhiteSpace(customer.Email))
            throw new DomainException("Add an email address to your profile first.");
        if (customer.IsEmailVerified) return BuildProfileResponse(customer);

        // A DomainException (400), not Unauthorized: the caller IS signed in, and a
        // 401 would send the apps' session refresher after a wrong code.
        if (!await otpService.VerifyAsync(UserType, "email", customer.Email, code, ct))
            throw new DomainException("That code is incorrect or has expired. Request a new one and try again.");

        customer.IsEmailVerified = true;
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
        IsEmailVerified = customer.IsEmailVerified,
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
