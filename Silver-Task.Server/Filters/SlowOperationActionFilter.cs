using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Filters
{
    /// <summary>Phase 60 — times every controller action; anything at or above
    /// Diagnostics:SlowOperationThresholdMs (default 1000ms, same Diagnostics:* config namespace
    /// Phase 58 established) gets recorded in ISlowOperationTracker. Registered globally
    /// (Program.cs, options.Filters.Add&lt;SlowOperationActionFilter&gt;()) — DI-activated per
    /// request, so it can depend on scoped/singleton services normally.</summary>
    public class SlowOperationActionFilter(ISlowOperationTracker tracker, IConfiguration configuration) : IAsyncActionFilter
    {
        private readonly ISlowOperationTracker _tracker = tracker;
        private readonly IConfiguration _configuration = configuration;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();
            await next();
            stopwatch.Stop();

            var thresholdMs = _configuration.GetValue("Diagnostics:SlowOperationThresholdMs", 1000);
            if (stopwatch.ElapsedMilliseconds < thresholdMs)
            {
                return;
            }

            var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
            var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
            _tracker.Record($"{controller}.{action}", stopwatch.ElapsedMilliseconds);
        }
    }
}
