namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Phase 36 — bounds notification table growth per the spec's own "do not allow unlimited
    /// notification growth" instruction. Runs once a day (same PeriodicTimer + per-tick DI scope
    /// pattern every other background service in this app already uses) and purges rows older
    /// than the admin-configured Notifications.RetentionDays system setting (default 90). Deletes
    /// purely by age, regardless of IsRead — the spec explicitly only asks to protect *recent*
    /// notifications from deletion, not unread ones specifically.
    /// </summary>
    public class NotificationRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IWorkerHeartbeatRegistry heartbeats,
        ILogger<NotificationRetentionBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        private const string WorkerName = "notification-retention";

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IWorkerHeartbeatRegistry _heartbeats = heartbeats;
        private readonly ILogger<NotificationRetentionBackgroundService> _logger = logger;

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
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.PurgeExpiredAsync();
                _heartbeats.ReportSuccess(WorkerName, Interval);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notification retention sweep failed.");
            }
        }
    }
}
