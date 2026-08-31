namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Phase 45 — consumes the EmailDeliveries queue NotificationService.MaybeSendEmailAsync
    /// writes to. Same PeriodicTimer + per-tick DI scope pattern as every other background
    /// service in this app (DueDateNotificationBackgroundService et al.) — a short interval (20s)
    /// is appropriate here since, unlike the due-date/digest sweeps, this is the primary delivery
    /// path for "immediate" email and users reasonably expect it to feel prompt, not scan-once-
    /// per-15-minutes like the due-date sweep.
    ///
    /// Deliveries claimed in the same tick are grouped by (RecipientUserId, NotificationType)
    /// before sending — see IEmailDeliveryService.AttemptGroupedDeliveryAsync's own doc comment
    /// for why this is the (intentionally lightweight) answer to the spec's "do not flood a user
    /// with near-identical emails from a bulk operation" requirement.
    /// </summary>
    public class EmailDeliveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailDeliveryBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);
        private const int BatchSize = 50;

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<EmailDeliveryBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            await RunTickAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunTickAsync(stoppingToken);
            }
        }

        private async Task RunTickAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IEmailDeliveryService>();

                var due = await deliveryService.ClaimDueAsync(BatchSize, stoppingToken);
                if (due.Count == 0)
                {
                    return;
                }

                var groups = due.GroupBy(d => (d.RecipientUserId, d.NotificationType));
                foreach (var group in groups)
                {
                    var items = group.ToList();
                    if (items.Count > 1)
                    {
                        await deliveryService.AttemptGroupedDeliveryAsync(items, stoppingToken);
                    }
                    else
                    {
                        await deliveryService.AttemptDeliveryAsync(items[0], stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // One bad tick (e.g. a transient DB blip) must never take down the whole worker —
                // the next tick 20 seconds later just tries again.
                _logger.LogError(ex, "Email delivery sweep failed.");
            }
        }
    }
}
