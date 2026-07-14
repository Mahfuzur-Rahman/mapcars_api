using System.Security.Claims;
using Mapcars.Application.Payments.Dtos;
using Mapcars.Application.Payments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Driver-only payout onboarding/status/history via Stripe Connect. Driver-scoped
/// — a rider token can never reach this (see [Authorize(Roles = "driver")]).
/// </summary>
[ApiController]
[Route("api/v1/driver")]
[Authorize(Roles = "driver")]
public class DriverPayoutsController : ControllerBase
{
    private readonly IPayoutService _payouts;

    public DriverPayoutsController(IPayoutService payouts) => _payouts = payouts;

    [HttpPost("payout-account/onboarding-link")]
    [ProducesResponseType(typeof(OnboardingLinkResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingLinkResponse>> CreateOnboardingLink(
        [FromBody] StartOnboardingRequest request, CancellationToken ct)
    {
        var driverId = CurrentDriverId();
        if (driverId is null) return Unauthorized();

        var email = User.FindFirstValue("identifier");
        var driverEmail = email is not null && email.Contains('@') ? email : null;

        return Ok(await _payouts.StartOnboardingAsync(
            driverId.Value, driverEmail, request.RefreshUrl, request.ReturnUrl, ct));
    }

    [HttpGet("payout-account")]
    [ProducesResponseType(typeof(PayoutAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayoutAccountResponse>> GetAccountStatus(CancellationToken ct)
    {
        var driverId = CurrentDriverId();
        if (driverId is null) return Unauthorized();

        return Ok(await _payouts.GetAccountStatusAsync(driverId.Value, ct));
    }

    [HttpGet("payouts")]
    [ProducesResponseType(typeof(IReadOnlyList<PayoutResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayoutResponse>>> ListPayouts(CancellationToken ct)
    {
        var driverId = CurrentDriverId();
        if (driverId is null) return Unauthorized();

        return Ok(await _payouts.ListPayoutsAsync(driverId.Value, ct));
    }

    private Guid? CurrentDriverId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
