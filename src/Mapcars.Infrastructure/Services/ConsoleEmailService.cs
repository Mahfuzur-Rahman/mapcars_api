using Mapcars.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Development email stub — logs emails to the console.
/// Replace with a real SendGrid/SES implementation in production.
/// </summary>
public class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public string ProviderName => "console";
    public string DefaultFromAddress => "no-reply@mapcars.uk";
    public string DefaultFromName => "MAP CARS";

    public Task SendAsync(
        string toEmail, string subject, string body, EmailSendOptions? options = null, CancellationToken ct = default)
    {
        var fromAddress = options?.FromAddress ?? DefaultFromAddress;
        logger.LogWarning(
            "[DEV EMAIL] From: {From} | To: {Email} | Subject: {Subject} | {Body}", fromAddress, toEmail, subject, body);
        return Task.CompletedTask;
    }
}
