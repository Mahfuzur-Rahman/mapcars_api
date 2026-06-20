using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        service = "Mapcars.Api"
    });
}
