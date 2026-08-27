namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Consumes AutomationDispatcher's Channel and processes each dispatched event via a scoped
    /// IAutomationService — this is what keeps automation evaluation off the normal request path
    /// (see the spec's own "do not execute expensive automation chains synchronously during
    /// normal API requests" requirement). Unlike RecurringTaskGenerationBackgroundService/
    /// DueDateNotificationBackgroundService (which poll on a PeriodicTimer), this reacts
    /// immediately to each enqueued event via ChannelReader.ReadAllAsync — appropriate here since
    /// automation processing is event-driven, not time-based (see
    /// AutomationOverdueCheckBackgroundService for the one trigger that genuinely is time-based).
    ///
    /// A BackgroundService is a singleton, so it can't hold a scoped AppDbContext/IAutomationService
    /// directly — a fresh DI scope is created per dequeued event, same pattern every other
    /// background service in this app already uses.
    /// </summary>
    public class AutomationQueueBackgroundService(
        IAutomationEventQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationQueueBackgroundService> logger) : BackgroundService
    {
        private readonly IAutomationEventQueue _queue = queue;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<AutomationQueueBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var envelope in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
                    await automationService.ProcessEventAsync(envelope);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // One bad event must never take down the whole consumer loop — the next
                    // enqueued event still gets processed normally.
                    _logger.LogError(ex, "Failed to process automation event {EventId} ({TriggerType}).", envelope.EventId, envelope.Event.TriggerType);
                }
            }
        }
    }
}
