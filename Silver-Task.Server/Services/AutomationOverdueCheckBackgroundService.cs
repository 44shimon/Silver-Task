using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common.Automation;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    /// <summary>
    /// Periodically sweeps for tasks that just became overdue and dispatches a TaskOverdueEvent
    /// for each — the "Task Becomes Overdue" automation trigger is inherently time-based (nothing
    /// a user does directly causes it), so unlike every other trigger it needs its own scheduled
    /// check rather than an inline dispatch call in some service method. Deliberately a separate
    /// class from DueDateNotificationBackgroundService (which already sweeps for overdue tasks,
    /// but for a different purpose — user-facing notifications, deduped against the Notifications
    /// table) rather than folding this into it: same shared PeriodicTimer/per-tick-scope pattern,
    /// but a distinct responsibility, matching how RecurringTaskGenerationBackgroundService is
    /// already its own class alongside DueDateNotificationBackgroundService rather than one
    /// do-everything service.
    ///
    /// TaskItem.OverdueAutomationProcessedAt is the once-per-transition guard (set here, cleared
    /// by TaskService.UpdateAsync whenever DueDate changes) — this is what satisfies the spec's
    /// "do not create duplicate overdue events every minute" requirement; without it, every sweep
    /// would re-fire the trigger for every still-overdue task, every 15 minutes, forever.
    /// </summary>
    public class AutomationOverdueCheckBackgroundService(
        IServiceScopeFactory scopeFactory,
        IWorkerHeartbeatRegistry heartbeats,
        ILogger<AutomationOverdueCheckBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private const string WorkerName = "automation-overdue-check";

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IWorkerHeartbeatRegistry _heartbeats = heartbeats;
        private readonly ILogger<AutomationOverdueCheckBackgroundService> _logger = logger;

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
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IAutomationDispatcher>();

                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                var candidates = await db.Tasks
                    .Where(t =>
                        t.OverdueAutomationProcessedAt == null &&
                        t.DueDate != null &&
                        t.DueDate < today &&
                        t.Status != TaskItemStatus.Complete &&
                        t.Status != TaskItemStatus.Cancelled)
                    .Select(t => new { t.Id, t.ProjectId, DueDate = t.DueDate!.Value })
                    .ToListAsync(stoppingToken);

                foreach (var task in candidates)
                {
                    await dispatcher.DispatchAsync(new TaskOverdueEvent(task.Id, task.ProjectId, task.DueDate, DateTime.UtcNow));
                }

                if (candidates.Count > 0)
                {
                    var ids = candidates.Select(c => c.Id).ToList();
                    await db.Tasks
                        .Where(t => ids.Contains(t.Id))
                        .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.OverdueAutomationProcessedAt, DateTime.UtcNow), stoppingToken);
                }
                _heartbeats.ReportSuccess(WorkerName, Interval);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Automation overdue-check sweep failed.");
            }
        }
    }
}
