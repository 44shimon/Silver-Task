namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Periodically sweeps for tasks that just became due-soon/overdue and raises the
    /// corresponding notifications. Runs independently of any page load — the alternative
    /// (checking on every "get my tasks" request) would tie notification freshness to someone
    /// happening to open the app, and could easily turn into "recompute on every request" if not
    /// carefully deduplicated. INotificationService.CreateDueSoonAndOverdueNotificationsAsync
    /// already dedupes per (user, task, type), so running this on a timer is simply "how often do
    /// we notice," never a source of duplicate notifications.
    ///
    /// A BackgroundService is a singleton, so it can't hold a scoped AppDbContext/
    /// INotificationService directly — each tick creates its own DI scope, same as any other
    /// long-lived singleton that needs scoped services.
    /// </summary>
    public class DueDateNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DueDateNotificationBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<DueDateNotificationBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            // Run once immediately on startup, then on the timer — otherwise a fresh deploy
            // would wait a full interval before the first sweep.
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
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.CreateDueSoonAndOverdueNotificationsAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A failed sweep should never crash the app — it'll simply try again next
                // interval, and CreateDueSoonAndOverdueNotificationsAsync's own dedup means a
                // partially-completed sweep can't produce duplicates on retry.
                _logger.LogError(ex, "Due-soon/overdue notification sweep failed.");
            }
        }
    }
}
