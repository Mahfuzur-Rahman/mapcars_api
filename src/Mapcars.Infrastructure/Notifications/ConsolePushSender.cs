using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Notifications;

/// <summary>
/// Fallback push transport used when Firebase isn't configured — logs instead of
/// sending, so the whole registration/notify flow is exercisable in dev without
/// credentials. Never reports a token as invalid.
/// </summary>
public class ConsolePushSender(ILogger<ConsolePushSender> logger) : IPushSender
{
    public Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[push:stub] → {Count} device(s): \"{Title}\" — {Body}",
            tokens.Count, message.Title, message.Body);
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
