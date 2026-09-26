using System.Security.Claims;
using Mapcars.Application.Settings.Dtos;
using Mapcars.Application.Settings.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Which payment methods the platform accepts.
///
/// <para>
/// Reading is public, for the same reason the fare chart is: both apps need it at
/// launch, before anyone signs in, to decide what to render on the booking sheet.
/// Nothing here is sensitive — it is the same information a customer sees as two
/// buttons.
/// </para>
///
/// <para>
/// Editing is SuperAdmin-only, mirroring the fare chart. Turning card payments off
/// platform-wide has the same blast radius as republishing prices.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/payment-settings")]
public class PaymentSettingsController(IPaymentSettingsService settings) : ControllerBase
{
    /// <summary>The current global payment settings.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentSettingsResponse>> Get(CancellationToken ct)
        => Ok(await settings.GetAsync(ct));

    /// <summary>The full settings document, including the fraud thresholds.</summary>
    [HttpGet("admin")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(AdminPaymentSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPaymentSettingsResponse>> GetForAdmin(CancellationToken ct)
        => Ok(await settings.GetForAdminAsync(ct));

    /// <summary>
    /// Publish new global settings. At least one method must stay enabled; a
    /// default naming a now-disabled method is corrected rather than rejected.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(AdminPaymentSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminPaymentSettingsResponse>> Update(
        [FromBody] UpdatePaymentSettingsRequest request, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();
        return Ok(await settings.UpdateAsync(request, adminId, ct));
    }

    /// <summary>One driver's payment overrides, with the effective result resolved.</summary>
    [HttpGet("drivers/{driverId:guid}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(DriverPaymentOptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverPaymentOptionsResponse>> GetDriverOptions(
        Guid driverId, CancellationToken ct)
        => Ok(await settings.GetDriverOptionsAsync(driverId, ct));

    /// <summary>
    /// Set one driver's overrides. Null clears an override back to "follow the
    /// global setting"; the tri-state is carried on the wire on purpose.
    /// </summary>
    [HttpPut("drivers/{driverId:guid}")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(DriverPaymentOptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverPaymentOptionsResponse>> UpdateDriverOptions(
        Guid driverId, [FromBody] UpdateDriverPaymentOptionsRequest request, CancellationToken ct)
        => Ok(await settings.UpdateDriverOptionsAsync(driverId, request, ct));

    private bool TryGetAdminId(out Guid adminId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);
}
