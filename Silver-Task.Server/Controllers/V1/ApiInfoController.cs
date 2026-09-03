using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Data;

namespace Silver_Task.Server.Controllers.V1
{
    /// <summary>Phase 61 — API version metadata and a v1-scoped health check for the public API
    /// foundation. Both anonymous, same reasoning as the existing internal HealthController
    /// (Controllers/HealthController.cs): a health/meta endpoint that itself requires auth defeats
    /// its own purpose for unauthenticated tooling. Deliberately additive — /api/health and
    /// /api/health/ready (which scripts/update-debian.sh and infra probes already depend on) are
    /// completely untouched.</summary>
    [ApiController]
    [Route("api/v1")]
    [AllowAnonymous]
    public class ApiInfoController(AppDbContext db) : ControllerBase
    {
        private readonly AppDbContext _db = db;

        private const string ApiVersion = "v1";
        private static readonly string[] SupportedVersions = [ApiVersion];

        private static readonly string AppVersion =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        [HttpGet("meta")]
        public IActionResult GetMeta() => Ok(new
        {
            apiVersion = ApiVersion,
            appVersion = AppVersion,
            supportedVersions = SupportedVersions
        });

        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            var canConnect = await _db.Database.CanConnectAsync();
            if (!canConnect)
            {
                return StatusCode(503, new { status = "unavailable", database = "unreachable", timeUtc = DateTime.UtcNow });
            }
            return Ok(new { status = "ok", database = "reachable", timeUtc = DateTime.UtcNow });
        }
    }
}
