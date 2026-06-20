using Mapcars.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Development email stub — logs emails to the console.
/// Replace with a real SendGrid/SES implementation in production.
/// </summary>
public class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        logger.LogWarning("[DEV EMAIL] To: {Email} | Subject: {Subject} | {Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
