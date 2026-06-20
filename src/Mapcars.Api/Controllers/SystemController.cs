using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Versioned connectivity check used by the web and mobile apps to confirm they
/// can reach the API. Does not touch the database.
/// </summary>
[ApiController]
[Route("api/v1")]
public class SystemController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new
    {
        message = "pong from Mapcars API",
        utc = DateTime.UtcNow
    });
}
