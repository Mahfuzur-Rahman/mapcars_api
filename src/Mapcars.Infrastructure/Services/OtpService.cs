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
        var now = DateTime.UtcNow;
        var record = await db.VerificationCodes
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

        if (!FixedTimeEquals(record.Code, code))
        {
            record.AttemptCount++;
            if (record.AttemptCount >= IOtpService.MaxAttempts)
            {
                record.UsedAt = now; // burnt — the next attempt finds no live code
                logger.LogWarning("OTP for {Provider}:{Identifier} burnt after {Attempts} wrong attempts",
                    provider, identifier, record.AttemptCount);
            }
            await db.SaveChangesAsync(ct);
            return false;
        }

        record.UsedAt = now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var diff = expected.Length ^ actual.Length;
        for (var i = 0; i < expected.Length && i < actual.Length; i++)
            diff |= expected[i] ^ actual[i];
        return diff == 0;
    }

    private async Task<string> CreateCodeAsync(
        string userType, string provider, string identifier, int expiryMinutes, string sentVia, CancellationToken ct)
    {
        // Invalidate any existing unused codes for this identifier
        var old = await db.VerificationCodes
            .Where(v => v.Provider == provider && v.Identifier == identifier && v.UsedAt == null)
            .ToListAsync(ct);

        foreach (var o in old) o.UsedAt = DateTime.UtcNow.AddYears(-1); // mark stale

        var code = Random.Shared.Next(100000, 999999).ToString();
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
