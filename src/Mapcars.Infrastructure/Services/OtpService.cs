using Mapcars.Application.Common;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

public class OtpService(
    AppDbContext db,
    ISmsService sms,
    IEmailService email,
    ILogger<OtpService> logger) : IOtpService
{
    private const int PhoneExpiryMinutes = 5;
    private const int EmailExpiryMinutes = 3;

    // Long enough to find the email and switch apps, short enough that a code
    // sitting in an inbox isn't a standing key to the account.
    private const int ResetExpiryMinutes = 15;

    public async Task<string> CreateAndSendPhoneOtpAsync(string userType, string phone, CancellationToken ct = default)
    {
        var code = await CreateCodeAsync(userType, "phone", phone, PhoneExpiryMinutes, sms.ProviderName, ct);
        // Delivery is best-effort: the code is already persisted, so a provider
        // outage must not fail the request (the caller returns devCode too).
        try
        {
            await sms.SendAsync(phone, $"Your MAP CARS code is {code}. Valid for {PhoneExpiryMinutes} minutes.", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS OTP to {Phone}", phone);
        }
        return code;
    }

    public async Task<string> CreateAndSendEmailOtpAsync(string userType, string emailAddr, CancellationToken ct = default)
    {
        var code = await CreateCodeAsync(userType, "email", emailAddr, EmailExpiryMinutes, email.ProviderName, ct);
        try
        {
            await email.SendAsync(
                emailAddr,
                "Your MAP CARS verification code",
                $"Your verification code is <strong>{code}</strong>. It expires in {EmailExpiryMinutes} minutes.",
                ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email OTP to {Email}", emailAddr);
        }
        return code;
    }

    public async Task CreateAndSendPasswordResetAsync(string userType, string emailAddr, CancellationToken ct = default)
    {
        var code = await CreateCodeAsync(
            userType, IOtpService.ResetProvider, emailAddr, ResetExpiryMinutes, email.ProviderName, ct);
        try
        {
            await email.SendAsync(
                emailAddr,
                "Reset your MAP CARS password",
                $"Your password reset code is <strong>{code}</strong>. It expires in {ResetExpiryMinutes} minutes." +
                "<br><br>If you didn't ask to reset your password, you can ignore this email — " +
                "your password hasn't changed.",
                ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to {Email}", emailAddr);
        }
    }

    public async Task<bool> VerifyAsync(string userType, string provider, string identifier, string code, CancellationToken ct = default)
    {
        // The live code: CreateCodeAsync retires older ones, so there is at most
        // one. Look it up WITHOUT matching the code, so a wrong guess can be
        // counted against it — matching on the code meant a guess that missed
        // found nothing to count, and guessing was unlimited for the code's
        // whole lifetime.
        //
        // Untracked, and every write below is an ExecuteUpdate: those bypass the
        // change tracker, so a tracked copy would silently go stale under them.
        var now = DateTime.UtcNow;
        var record = await db.VerificationCodes
            .AsNoTracking()
            .Where(v =>
                v.UserType == userType &&
                v.Provider == provider &&
                v.Identifier == identifier &&
                v.UsedAt == null &&
                v.ExpiresAt > now)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (record is null)
        {
            logger.LogWarning("OTP verification failed for {Provider}:{Identifier} (no live code)", provider, identifier);
            return false;
        }

        // Spend an attempt BEFORE looking at the guess, and only while under the
        // cap — one conditional UPDATE, so the database does check-and-increment
        // atomically. Reading the count, bumping it in memory and saving let a
        // burst of parallel wrong guesses all read the same count and all get
        // compared, so the cap could be outrun. Every guess counts, the right one
        // included: the MaxAttempts-th guess is the last one ever compared, and a
        // code at the cap stays dead for its remaining life (no burn needed).
        var granted = await db.VerificationCodes
            .Where(v => v.Id == record.Id)
            .Where(OtpCode.MayAttempt(now))
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.AttemptCount, v => v.AttemptCount + 1), ct);

        if (granted == 0)
        {
            // At the cap — or used/expired by a parallel request since the read.
            logger.LogWarning("OTP for {Provider}:{Identifier} refused — {Max} attempts spent or no longer live",
                provider, identifier, IOtpService.MaxAttempts);
            return false;
        }

        if (!OtpCode.Matches(record.Code, code)) return false;

        await db.VerificationCodes
            .Where(v => v.Id == record.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedAt, now), ct);
        return true;
    }

    private async Task<string> CreateCodeAsync(
        string userType, string provider, string identifier, int expiryMinutes, string sentVia, CancellationToken ct)
    {
        // Invalidate any existing unused codes for this identifier
        var old = await db.VerificationCodes
            .Where(v => v.Provider == provider && v.Identifier == identifier && v.UsedAt == null)
            .ToListAsync(ct);

        foreach (var o in old) o.UsedAt = DateTime.UtcNow.AddYears(-1); // mark stale

        var code = OtpCode.Generate();
        db.VerificationCodes.Add(new VerificationCode
        {
            Id = Guid.NewGuid(),
            UserType = userType,
            Provider = provider,
            Identifier = identifier,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            CreatedAt = DateTime.UtcNow,
            SentVia = sentVia,
        });

        await db.SaveChangesAsync(ct);
        return code;
    }
}
