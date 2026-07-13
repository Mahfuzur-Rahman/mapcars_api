using System.Text.Json;
using Mapcars.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>Receives Resend's inbound-email webhook and forwards each message to the configured mailbox.</summary>
[ApiController]
[Route("api/v1/webhooks/resend")]
public class ResendWebhookController(IInboundEmailService inbound, ILogger<ResendWebhookController> logger) : ControllerBase
{
    [HttpPost("inbound")]
    public async Task<IActionResult> Inbound(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var svixId = Request.Headers["svix-id"].ToString();
        var svixTimestamp = Request.Headers["svix-timestamp"].ToString();
        var svixSignature = Request.Headers["svix-signature"].ToString();

        if (!inbound.VerifySignature(rawBody, svixId, svixTimestamp, svixSignature))
        {
            logger.LogWarning("Rejected Resend webhook with invalid signature");
            return Unauthorized();
        }

        using var doc = JsonDocument.Parse(rawBody);
        var type = doc.RootElement.GetProperty("type").GetString();

        if (type == "email.received")
        {
            var emailId = doc.RootElement.GetProperty("data").GetProperty("email_id").GetString();
            if (!string.IsNullOrEmpty(emailId))
            {
                await inbound.ForwardReceivedEmailAsync(emailId, ct);
            }
        }

        return Ok();
    }
}
