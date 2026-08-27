using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Models.Entities
{
    /// <summary>A single in-app notification for one user. Deliberately not an EAV table like
    /// CustomFields/SystemSettings — Type is still free text (see Common.NotificationTypes) so a
    /// new type never needs a migration, but the row shape itself (Title/Message/TaskId/
    /// ProjectId/read state) is fixed because every notification genuinely has all of these
    /// fields, unlike a custom field's per-type-varying value.</summary>
    public class Notification
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string Type { get; set; }

        public required string Title { get; set; }

        public required string Message { get; set; }

        /// <summary>Phase 36 — closed 3-level severity, resolved automatically per Type by
        /// NotificationService.NotifyAsync unless a caller overrides it. Drives the visual
        /// distinction (dot color/urgency badge) in the notification center.</summary>
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        /// <summary>Phase 36 — who caused this notification (already passed to NotifyAsync at
        /// every call site, just never persisted before now). Null for system/background-sweep-
        /// originated notifications (due-soon/overdue sweeps, digest) that have no human actor.
        /// SetNull on delete — the message text already carries the actor's name, so losing the
        /// link just means "who did this" can no longer be resolved to a live user record.</summary>
        public Guid? ActorUserId { get; set; }

        /// <summary>Nullable and SetNull-on-delete — a task can be deleted after a notification
        /// about it was created; the notification's Title/Message already carry the human-
        /// readable text, so losing the link just means "open task" degrades gracefully instead
        /// of the notification itself being destroyed.</summary>
        public Guid? TaskId { get; set; }

        public Guid? ProjectId { get; set; }

        /// <summary>Phase 36 — set for comment/mention notifications, SetNull on delete.</summary>
        public Guid? CommentId { get; set; }

        /// <summary>Phase 36 — set for file-upload notifications, SetNull on delete.</summary>
        public Guid? FileId { get; set; }

        /// <summary>Phase 36 — precomputed deep link the frontend can navigate to directly
        /// (resolved once, server-side, by NotifyAsync from whichever of Task/Project/Comment/
        /// File ids were supplied) rather than every UI surface re-deriving the same routing
        /// logic. Deliberately still just a path, not a full URL/host — the destination route
        /// itself always re-enforces authorization on load (see NotificationsController's own
        /// doc comment on why a notification can never be used to bypass permissions).</summary>
        public string? ActionUrl { get; set; }

        /// <summary>Phase 36 — an idempotency key an opt-in caller supplies (e.g. an automation's
        /// dispatched envelope id, or a file upload's own attachment id) so the same underlying
        /// occurrence can never produce two notifications for the same (user, type) pair even if
        /// the triggering code path runs twice (retry, duplicate event delivery). Most call sites
        /// don't need this — see NotifyAsync's own doc comment.</summary>
        public Guid? EventId { get; set; }

        /// <summary>Small JSON blob for anything a notification type needs beyond Title/Message
        /// (e.g. the old/new value for a field-change notification) — optional, most types don't
        /// need it since the message text already says everything relevant.</summary>
        public string? Metadata { get; set; }

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public User? ActorUser { get; set; }

        public TaskItem? Task { get; set; }

        public Project? Project { get; set; }

        public TaskComment? Comment { get; set; }

        public Attachment? File { get; set; }
    }
}
