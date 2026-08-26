namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Periodically materializes the next due task occurrence(s) for every active recurring
    /// series. Modeled directly on DueDateNotificationBackgroundService (same PeriodicTimer +
    /// per-tick DI scope + run-once-on-startup shape) — the only hosted-service pattern this app
    /// already has, reused rather than introducing Hangfire/Quartz for a single periodic job.
    ///
    /// Runs more often than the 15-minute due-date sweep (every 5 minutes) since a newly-created
    /// recurring task's first *future* occurrence should appear promptly rather than users waiting
    /// up to a quarter hour to see it — generation itself is cheap (see
    /// RecurringTaskService.GenerateDueOccurrencesAsync's single indexed due-rule query) so the
    /// tighter interval costs little.
    /// </summary>
    public class RecurringTaskGenerationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringTaskGenerationBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<RecurringTaskGenerationBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            await RunSweepAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSweepAsync(stoppingToken);
            }
        }

        private async Task RunSweepAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var recurringTaskService = scope.ServiceProvider.GetRequiredService<IRecurringTaskService>();
                var generatedCount = await recurringTaskService.GenerateDueOccurrencesAsync();
                if (generatedCount > 0)
                {
                    _logger.LogInformation("Recurring task sweep generated {Count} occurrence(s).", generatedCount);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // GenerateDueOccurrencesAsync already isolates per-rule failures internally; this
                // is the outer backstop in case something fails before/between rules (e.g. the
                // due-rule query itself) — never let a failed sweep crash the host.
                _logger.LogError(ex, "Recurring task generation sweep failed.");
            }
        }
    }
}
