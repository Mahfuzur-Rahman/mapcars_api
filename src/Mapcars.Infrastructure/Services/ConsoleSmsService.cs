using Mapcars.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Development SMS stub — logs messages to the console.
/// Replace with a real Twilio implementation in production.
/// </summary>
public class ConsoleSmsService(ILogger<ConsoleSmsService> logger) : ISmsService
{
    public string ProviderName => "console";

    public Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        logger.LogWarning("[DEV SMS] To: {Phone} | {Message}", toPhone, message);
        return Task.CompletedTask;
    }
}
