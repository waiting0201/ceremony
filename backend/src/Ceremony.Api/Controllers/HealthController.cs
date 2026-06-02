using Ceremony.Infrastructure.Persistence;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous] // Electron 殼在登入前 ping /health 確認 sidecar 起來 + DB 連得上（兼作連線測試）
public sealed class HealthController(IDbConnectionFactory factory, ILogger<HealthController> log) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            await using var conn = await factory.CreateOpenAsync(ct);
            var result = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
            return Ok(new { status = "healthy", db = result == 1 ? "up" : "unknown" });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                db = "down",
                error = ex.Message,
            });
        }
    }
}
