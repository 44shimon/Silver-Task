using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Silver_Task.Server.Hubs;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data
{
    /// <summary>
    /// The bridge between "a Notification row was added somewhere" and "push it to that user's
    /// browser" — deliberately an EF Core SaveChangesInterceptor rather than a change to
    /// NotificationService.NotifyAsync itself, because NotifyAsync never calls SaveChangesAsync
    /// (see its own doc comment: it adds to the caller's existing unit of work so the notification
    /// and the change it's about commit atomically). A push fired from inside NotifyAsync would
    /// race the caller's own save — the client could refetch before the row actually exists. An
    /// interceptor instead observes the *real* commit point regardless of which of the ~15 call
    /// sites across TaskService/CommentService/ProjectService/AttachmentService/AutomationService
    /// triggered it, with zero changes needed to any of them.
    ///
    /// Registered per-DbContext-instance (see Program.cs's AddDbContext factory overload, which
    /// constructs a fresh interceptor for every scoped DbContext) — instance state here
    /// (_pendingUserIds) is safe precisely because it is never shared across concurrent
    /// requests/scopes.
    /// </summary>
    public class NotificationPushInterceptor(IHubContext<NotificationHub> hubContext) : SaveChangesInterceptor
    {
        private readonly IHubContext<NotificationHub> _hubContext = hubContext;
        private List<Guid> _pendingUserIds = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            _pendingUserIds = eventData.Context?.ChangeTracker.Entries<Notification>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity.UserId)
                .Distinct()
                .ToList() ?? [];

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var userIds = _pendingUserIds;
            _pendingUserIds = [];

            foreach (var userId in userIds)
            {
                // Best-effort — a disconnected/absent client simply relies on the existing 60s
                // poll to catch up (see NotificationHub's own doc comment); a push failure must
                // never surface as an error on the request that created the notification.
                try
                {
                    await _hubContext.Clients.Group(NotificationHub.GroupName(userId))
                        .SendAsync("notificationReceived", cancellationToken);
                }
                catch
                {
                    // Intentionally swallowed — see above.
                }
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }
}
