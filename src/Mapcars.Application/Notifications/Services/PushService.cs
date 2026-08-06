using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Mapcars.Application.Notifications.Services;

/// <summary>
/// Orchestrates push: stores tokens and fans a message out to a user's devices,
/// pruning any the transport reports as permanently invalid. Notifications are
/// best-effort — every failure is swallowed (logged) so a push never breaks the
/// trip flow that triggered it.
/// </summary>
public class PushService(
    IDeviceTokenRepository tokens,
    IPushSender sender,
    IUnitOfWork uow,
    ILogger<PushService> logger) : IPushService
{
    public async Task RegisterAsync(
        string userType, Guid userId, RegisterDeviceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) return;

        await tokens.UpsertAsync(new DeviceToken
        {
            UserType = userType,
            UserId = userId,
            Token = request.Token.Trim(),
            Platform = request.Platform,
        }, ct);
        await uow.SaveChangesAsync(ct);
    }

    public async Task UnregisterAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        await tokens.RemoveByTokenAsync(token.Trim(), ct);
        await uow.SaveChangesAsync(ct);
    }

    public async Task NotifyUserAsync(
        string userType, Guid userId, PushMessage message, CancellationToken ct = default)
    {
        try
        {
            var userTokens = await tokens.ListTokensForUserAsync(userType, userId, ct);
            if (userTokens.Count == 0) return;

            var invalid = await sender.SendAsync(userTokens, message, ct);
            if (invalid.Count > 0)
            {
                await tokens.RemoveTokensAsync(invalid, ct);
                await uow.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push notify failed for {UserType} {UserId}", userType, userId);
        }
    }
}
