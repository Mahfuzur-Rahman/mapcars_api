using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IConnectionMultiplexer? _redis;

    // Optional on purpose: the API boots (and prices from Postgres) without Redis.
    public HealthController(IConnectionMultiplexer? redis = null) => _redis = redis;

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        service = "Mapcars.Api",
        // Redis is the live-driver hot path. When it is missing, driver location
        // pushes are silently dropped and `/drivers/nearby` returns an empty list —
        // the rider map looks fine but shows no cars. Surface it here so that
        // failure is one curl away instead of a debugging session.
        redis = _redis switch
        {
            null => "not-configured",
            { IsConnected: true } => "connected",
            _ => "disconnected",
        }
    });
}
