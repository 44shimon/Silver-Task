using System.Text.Json;

namespace Silver_Task.Server.Middleware
{
    /// <summary>
    /// Phase 54 — checked first in the pipeline (before ExceptionHandlingMiddleware, static
    /// assets, auth — everything), so a maintenance window blocks the SPA shell and every API
    /// call alike, not just authenticated routes. Reads Maintenance:FlagFile (empty/unset =
    /// disabled, the safe default anywhere it isn't explicitly configured) and, if that file
    /// exists, returns 503 for every request except GET /api/health* — those must keep working
    /// so scripts/update-debian.sh's own health-check polling isn't itself blocked by the
    /// maintenance window it just entered. Deliberately reads only the file's *existence*, never
    /// its contents (upgradeId/target version/timestamps — see scripts/lib/upgrade.sh's
    /// st_up_maintenance_enable) — those stay server-side only; the public response is always the
    /// same generic message, never anything upgrade-specific.
    /// </summary>
    public class MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        private readonly RequestDelegate _next = next;
        private readonly string? _flagFilePath = configuration["Maintenance:FlagFile"];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task InvokeAsync(HttpContext context)
        {
            var isHealthCheck = context.Request.Path.StartsWithSegments("/api/health");
            var maintenanceActive = !string.IsNullOrEmpty(_flagFilePath) && File.Exists(_flagFilePath);

            if (isHealthCheck || !maintenanceActive)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "30";
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = "maintenance",
                message = "Silver Task is temporarily unavailable for a scheduled upgrade. Please try again shortly."
            }, JsonOptions));
        }
    }
}
