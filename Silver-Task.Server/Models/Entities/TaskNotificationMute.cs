namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 44 — a per-(user, task) opt-out from that one task's notifications ("Mute
    /// Notifications" on the task detail panel). Deliberately narrow: this mutes TASK-scoped
    /// notification types for this task only (assignment/status/priority/due-date/comment/
    /// completion/reopened changes on THIS task) — it never affects the user's own global
    /// NotificationType preferences (UserNotificationSetting), and per spec it does not suppress
    /// @mentions, which stay visible even on a muted task (see NotificationService.NotifyAsync's
    /// own mute check). Cascade on Task — a mute preference for a task that no longer exists has
    /// no meaning, unlike Notification's own historical-record SetNull behavior.</summary>
    public class TaskNotificationMute
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid TaskId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public TaskItem? Task { get; set; }
    }
}
