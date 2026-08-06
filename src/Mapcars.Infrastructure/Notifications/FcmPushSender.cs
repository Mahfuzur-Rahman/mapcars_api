using FirebaseAdmin.Messaging;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mapcars.Infrastructure.Notifications;

/// <summary>
/// Firebase Cloud Messaging transport (HTTP v1 via the Admin SDK). Assumes the
/// default <c>FirebaseApp</c> was initialised at startup with a service account
/// (see Infrastructure DI). Best-effort: catches send errors and reports back the
/// tokens FCM says are permanently invalid so they can be pruned.
/// </summary>
public class FcmPushSender(ILogger<FcmPushSender> logger) : IPushSender
{
    public async Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default)
    {
        if (tokens.Count == 0) return Array.Empty<string>();

        var multicast = new MulticastMessage
        {
            Tokens = tokens.ToList(),
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data?.ToDictionary(kv => kv.Key, kv => kv.Value),
        };

        var invalid = new List<string>();
        try
        {
            var response = await FirebaseMessaging.DefaultInstance
                .SendEachForMulticastAsync(multicast, ct);

            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                if (r.IsSuccess) continue;

                // Prune tokens the server considers dead — a stale install or a
                // token that no longer maps to this app.
                var code = r.Exception?.MessagingErrorCode;
                if (code is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                    invalid.Add(tokens[i]);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FCM multicast send failed for {Count} token(s)", tokens.Count);
        }

        return invalid;
    }
}
