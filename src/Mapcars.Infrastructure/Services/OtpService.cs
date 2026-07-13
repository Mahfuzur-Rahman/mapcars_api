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
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email OTP to {Email}", emailAddr);
        }
        return code;
    }

    public async Task<bool> VerifyAsync(string userType, string provider, string identifier, string code, CancellationToken ct = default)
    {
        var record = await db.VerificationCodes
            .Where(v =>
                v.UserType == userType &&
                v.Provider == provider &&
                v.Identifier == identifier &&
                v.Code == code &&
                v.UsedAt == null &&
                v.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (record is null)
        {
            logger.LogWarning("OTP verification failed for {Provider}:{Identifier}", provider, identifier);
            return false;
        }

        record.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
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
