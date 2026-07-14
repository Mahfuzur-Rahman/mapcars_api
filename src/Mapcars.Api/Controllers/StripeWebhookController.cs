using Mapcars.Application.Payments.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace Mapcars.Api.Controllers;

/// <summary>Receives Stripe Connect webhook events and syncs the local payout-account/payout cache.</summary>
[ApiController]
[Route("api/v1/webhooks/stripe")]
public class StripeWebhookController(
    IPayoutService payouts,
    IOptions<StripeOptions> options,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, options.Value.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Rejected Stripe webhook with invalid signature");
            return Unauthorized();
        }

        switch (stripeEvent.Type)
        {
            case "account.updated" when stripeEvent.Data.Object is Account account:
                await payouts.SyncAccountStatusAsync(
                    account.Id, account.DetailsSubmitted, account.PayoutsEnabled, account.ChargesEnabled, ct);
                break;

            case "payout.paid" or "payout.failed" or "payout.updated" or "payout.canceled"
                when stripeEvent.Data.Object is Payout payout && stripeEvent.Account is not null:
                // stripeEvent.Account (not the payout object) identifies which connected
                // account this event is about — this is a Connect webhook endpoint.
                await payouts.UpsertPayoutAsync(
                    stripeEvent.Account,
                    payout.Id,
                    payout.Amount / 100m,
                    payout.Currency,
                    MapStatus(payout.Status),
                    payout.ArrivalDate,
                    ct);
                break;
        }

        return Ok();
    }

    private static string MapStatus(string stripeStatus) => stripeStatus switch
    {
        "paid" => "Paid",
        "failed" => "Failed",
        "canceled" => "Canceled",
        "in_transit" => "InTransit",
        _ => "Pending",
    };
}
