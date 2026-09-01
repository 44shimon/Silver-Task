using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Data;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Phase 47 — split into liveness (process is up, no dependencies checked — safe for
    /// a tight load-balancer/orchestrator probe interval) and readiness (can this instance
    /// actually serve a request, i.e. can it reach the database — the check that matters for
    /// "should traffic be routed here"). Both anonymous: neither leaks anything beyond up/down and
    /// a timestamp, and a health endpoint that itself requires auth defeats its own purpose for
    /// unauthenticated infrastructure probes. Phase 51 adds `version` to the liveness response
    /// (the running assembly's own informational version, sourced from the repo-root VERSION
    /// file via Silver-Task.Server.csproj) rather than a separate endpoint — same disclosure tier
    /// as the existing status/timestamp, and lets scripts/update-debian.sh's post-update health
    /// check confirm the new version actually deployed without needing credentials.</summary>
    [ApiController]
    [Route("api/health")]
    [AllowAnonymous]
    public class HealthController(AppDbContext db) : ControllerBase
    {
        private readonly AppDbContext _db = db;

        private static readonly string AppVersion =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        [HttpGet]
        public IActionResult Get() => Ok(new { status = "ok", version = AppVersion, timeUtc = DateTime.UtcNow });

        [HttpGet("ready")]
        public async Task<IActionResult> GetReady()
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
