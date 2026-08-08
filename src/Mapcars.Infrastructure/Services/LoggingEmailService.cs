using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Emails.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Wraps whichever <see cref="IEmailService"/> provider is registered
/// (<see cref="ResendEmailService"/>, <see cref="SmtpEmailService"/>, or
/// <see cref="ConsoleEmailService"/>) and records every send attempt to
/// <see cref="EmailLog"/> — success or failure, System or Compose. This is the
/// single choke point every email passes through, so existing callers
/// (<c>OtpService</c>, <c>AdminAuthService</c>) get logged automatically
/// without any change on their part.
/// </summary>
public class LoggingEmailService(
    IEmailService inner,
    IEmailLogRepository logs,
    ILogger<LoggingEmailService> logger) : IEmailService
{
    public string ProviderName => inner.ProviderName;
    public string DefaultFromAddress => inner.DefaultFromAddress;
    public string DefaultFromName => inner.DefaultFromName;

    public async Task SendAsync(
        string toEmail, string subject, string body, EmailSendOptions? options = null, CancellationToken ct = default)
    {
        var fromAddress = options?.FromAddress ?? inner.DefaultFromAddress;
        var fromName = options?.FromName ?? inner.DefaultFromName;
        var category = options?.Category ?? "System";

        try
        {
            await inner.SendAsync(toEmail, subject, body, options, ct);
            await WriteLogAsync(toEmail, fromAddress, fromName, subject, body, category, options?.SentByAdminId, "Sent", null, ct);
        }
        catch (Exception ex)
        {
            await WriteLogAsync(toEmail, fromAddress, fromName, subject, body, category, options?.SentByAdminId, "Failed", ex.Message, ct);
            throw; // preserves today's behavior: existing callers still catch+log the failure themselves.
        }
    }

    // Logging never throws — a DB hiccup while recording a send must not turn
    // into a second failure on top of (or instead of) the one being recorded.
    private async Task WriteLogAsync(
        string toEmail, string fromAddress, string? fromName, string subject, string body,
        string category, Guid? sentByAdminId, string status, string? error, CancellationToken ct)
    {
        try
        {
            await logs.AddAsync(new EmailLog
            {
                ToEmail = toEmail,
                FromAddress = fromAddress,
                FromName = fromName,
                Subject = subject,
                BodyHtml = body,
                Provider = inner.ProviderName,
                Category = category,
                Status = status,
                ErrorMessage = error,
                SentByAdminId = sentByAdminId,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist an email log entry for {To}", toEmail);
        }
    }
}
