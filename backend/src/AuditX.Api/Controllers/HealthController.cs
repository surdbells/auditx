using AuditX.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AuditX.Api.Controllers;

/// <summary>Liveness/readiness surface for load balancers and operations (US-M15-027).</summary>
[AllowAnonymous]
[ApiController]
[Route("admin/health")]
public sealed class HealthController(AppDbContext db, IConnectionMultiplexer redis) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var database = await CanReachDatabaseAsync(cancellationToken);
        var cache = redis.IsConnected;
        var healthy = database && cache;

        var payload = new
        {
            status = healthy ? "healthy" : "unhealthy",
            checks = new { database, redis = cache },
            timestamp = DateTimeOffset.UtcNow,
        };

        return healthy ? Ok(payload) : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }

    private async Task<bool> CanReachDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }
}
