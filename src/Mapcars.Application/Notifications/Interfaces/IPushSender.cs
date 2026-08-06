using Mapcars.Application.Notifications.Dtos;

namespace Mapcars.Application.Notifications.Interfaces;

/// <summary>
/// Low-level push transport (FCM in production; a console stub when Firebase
/// isn't configured). Best-effort: never throws for a delivery failure.
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// Send <paramref name="message"/> to each token. Returns the tokens that are
    /// permanently invalid (unregistered / bad) so the caller can prune them.
    /// </summary>
    Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default);
}
