using Mapcars.Application.Contact.Dtos;
using Mapcars.Application.Contact.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

/// <summary>The public contact form on mapcars.uk.</summary>
[ApiController]
[Route("api/v1/contact")]
public class ContactController(IContactService contact) : ControllerBase
{
    /// <summary>
    /// Forwards an enquiry to the support inbox. Anonymous, and rate-limited as a
    /// billable send (5 per 15 minutes per client).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] ContactRequest request, CancellationToken ct)
    {
        await contact.SubmitAsync(request, ct);
        return Accepted();
    }
}
