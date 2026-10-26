using Mapcars.Application.Common;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Common.Phones;
using Mapcars.Application.ErrorLogs.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;
using Mapcars.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

public class OtpService(
    AppDbContext db,
    ISmsService sms,
    IEmailService email,
    PhoneLoginPolicy phonePolicy,
    IErrorLogService errorLogs,
    ILogger<OtpService> logger) : IOtpService
{
    private const int PhoneExpiryMinutes = 5;
    private const int EmailExpiryMinutes = 3;

    // Long enough to find the email and switch apps, short enough that a code
    // sitting in an inbox isn't a standing key to the account.
    private const int ResetExpiryMinutes = 15;

    /// <summary><c>verification_codes.sent_via</c> for a reserved test number's fixed code: never texted, never counted.</summary>
    private const string TestSentVia = "test";

    public async Task<string> CreateAndSendPhoneOtpAsync(string userType, string phone, CancellationToken ct = default)
    {
        // A reserved test number signs in with its fixed code. No SMS is sent,
        // no limits apply (store reviewers retry), and it isn't counted against
        // the daily cap.
        var testCode = phonePolicy.TestCodeFor(phone);
        if (testCode is not null)
            return await CreateCodeAsync(userType, "phone", phone, PhoneExpiryMinutes, TestSentVia, ct, fixedCode: testCode);

        // Limits run BEFORE a code is created, so a refused request never
        // retires the code the user is about to type.
        var now = DateTime.UtcNow;
        var dayAgo = now.AddDays(-1);
        var sentToNumber = await db.VerificationCodes.AsNoTracking()
            .Where(v => v.Provider == "phone" && v.Identifier == phone && v.CreatedAt > dayAgo)
            .Select(v => v.CreatedAt)
            .ToListAsync(ct);
        var sentPlatformWide = await db.VerificationCodes.AsNoTracking()
            .CountAsync(v => v.Provider == "phone" && v.CreatedAt > dayAgo && v.SentVia != TestSentVia, ct);

        var decision = PhoneSendGuard.Decide(now, sentToNumber, sentPlatformWide, phonePolicy.DailySendCap);
        if (decision.Refused)
        {
            if (decision.Refusal == SendRefusal.PlatformCap)
                await errorLogs.LogAsync(new ErrorLog
                {
                    Source = ErrorSource.Api,
                    Level = ErrorLevel.Error,
                    Message = $"SMS daily cap reached: {sentPlatformWide} codes in 24 h (cap {phonePolicy.DailySendCap}). Sign-in codes are being refused.",
                    Path = "sms/daily-cap",
                }, ct);
            throw new TooManyRequestsException(decision.RetryAfterSeconds, decision.Message);
        }

        var code = await CreateCodeAsync(userType, "phone", phone, PhoneExpiryMinutes, sms.ProviderName, ct);
        try
        {
            await sms.SendAsync(phone,
                $"Your MAP CARS code is {code}. It expires in {PhoneExpiryMinutes} minutes. Don't share it with anyone.", ct);
        }
        catch (Exception ex)
        {
            // Saying "code sent" when it wasn't leaves the user waiting for a
            // text that never comes. Kill the code, record why, and tell them.
            // The attempt still counts against the limits, so a failing number
            // can't be hammered.
            logger.LogError(ex, "SMS OTP delivery failed to {Phone}", MaskPhone(phone));
            await RetireLiveCodesAsync(userType, "phone", phone, ct);
            await errorLogs.LogAsync(new ErrorLog
            {
                Source = ErrorSource.Api,
                Level = ErrorLevel.Error,
                Message = $"SMS delivery failed to {MaskPhone(phone)} via {sms.ProviderName}: {ex.Message}",
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.ToString(),
                Path = "sms/send",
            }, ct);
            throw new DomainException("We couldn't send a code to that number. Check it and try again.");
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
                "Your MapCars verification code",
                EmailHtml.Paragraph("Enter this code to verify your email address:") +
                EmailHtml.Code(code) +
                EmailHtml.Muted(
                    $"It expires in {EmailExpiryMinutes} minutes. If you didn't ask for this code, " +
                    "you can safely ignore this email."),
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
                "Reset your MapCars password",
                EmailHtml.Paragraph("Use this code to reset your password:") +
                EmailHtml.Code(code) +
                EmailHtml.Muted(
                    $"It expires in {ResetExpiryMinutes} minutes. If you didn't ask to reset your password, " +
                    "you can ignore this email — your password hasn't changed."),
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
        string userType, string provider, string identifier, int expiryMinutes, string sentVia, CancellationToken ct,
        string? fixedCode = null)
    {
        // Retire this user type's unused codes for the identifier. Matching on
        // user type matters: a customer and a driver can share a phone, and a
        // customer's send-otp used to kill the driver's live code.
        var old = await db.VerificationCodes
            .Where(v => v.UserType == userType && v.Provider == provider && v.Identifier == identifier && v.UsedAt == null)
            .ToListAsync(ct);

        foreach (var o in old) o.UsedAt = DateTime.UtcNow.AddYears(-1); // mark stale

        var code = fixedCode ?? OtpCode.Generate();
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

    /// <summary>Marks every live code for this identifier used, so none can be verified.</summary>
    private Task<int> RetireLiveCodesAsync(string userType, string provider, string identifier, CancellationToken ct)
    {
        var stale = DateTime.UtcNow.AddYears(-1);
        return db.VerificationCodes
            .Where(v => v.UserType == userType && v.Provider == provider && v.Identifier == identifier && v.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedAt, stale), ct);
    }

    /// <summary>"+447400123456" becomes "+44*******456": enough to recognise in a log, not enough to text.</summary>
    private static string MaskPhone(string phone)
        => phone.Length > 6 ? phone[..3] + new string('*', phone.Length - 6) + phone[^3..] : "***";
}
