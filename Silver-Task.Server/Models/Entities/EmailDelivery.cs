using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Phase 45 — the email side of a notification event, queued instead of sent inline
    /// so a slow/unavailable SMTP server can never block the request that raised it (see
    /// EmailDeliveryBackgroundService). Deliberately self-contained (Title/Message/ActionUrl/
    /// ActorUserId/TaskId/ProjectId all snapshotted here directly) rather than hard-FK'd to a
    /// required Notification row — NotificationService.NotifyAsync's in-app and email channels
    /// are independently toggleable per notification type, so a type can have EmailEnabled=true
    /// with InAppEnabled=false, meaning no Notification row exists at all for some deliveries.
    /// NotificationId is kept as a best-effort, nullable cross-reference (SetNull, not Cascade,
    /// on delete) purely so the admin delivery log can link back to the in-app notification when
    /// one happens to exist — never something delivery itself depends on. TaskId/ProjectId are
    /// snapshotted independently of whatever Notification.TaskId ends up as later (which gets
    /// SetNull'd if the task/project is deleted) specifically so EmailDeliveryService can detect
    /// "this task existed when queued but was deleted before send" (see AttemptDeliveryAsync).</summary>
    public class EmailDelivery
    {
        public Guid Id { get; set; }

        public Guid? NotificationId { get; set; }

        public Guid RecipientUserId { get; set; }

        public required string RecipientEmail { get; set; }

        public required string NotificationType { get; set; }

        public Guid? ActorUserId { get; set; }

        public required string Title { get; set; }

        public required string Message { get; set; }

        public string? ActionUrl { get; set; }

        public Guid? TaskId { get; set; }

        public Guid? ProjectId { get; set; }

        /// <summary>Phase 46 — set only for digest rows (NotificationType "DailyDigest"/
        /// "WeeklyDigest"), where DigestGenerationService renders the full multi-section email
        /// once at generation time rather than per-attempt. When set, EmailDeliveryService skips
        /// the normal per-notification template rendering/entity-existence re-checks (already
        /// done, against a live query, at generation time — see that class's own doc comment) and
        /// sends this content directly, so a retry re-sends the exact same digest rather than
        /// re-scanning the window and risking a different result each attempt.</summary>
        public string? RenderedSubject { get; set; }

        public string? RenderedHtmlBody { get; set; }

        public EmailDeliveryStatus Status { get; set; } = EmailDeliveryStatus.Queued;

        public int AttemptCount { get; set; }

        /// <summary>A short, safe message only (exception text is never persisted verbatim —
        /// see EmailDeliveryService.ApplyResult/IEmailService.SendAsync) — this is admin-visible
        /// via the delivery log, so it must never be able to contain SMTP credentials or a raw
        /// stack trace.</summary>
        public string? LastError { get; set; }

        public DateTime QueuedAt { get; set; }

        /// <summary>When the worker should next attempt this row — set to QueuedAt on enqueue
        /// (deliver ASAP) and pushed forward on each failed attempt (retry backoff).</summary>
        public DateTime NextAttemptAt { get; set; }

        public DateTime? SentAt { get; set; }

        public DateTime? FailedAt { get; set; }

        public Notification? Notification { get; set; }

        public User? RecipientUser { get; set; }

        public User? ActorUser { get; set; }
    }
}
