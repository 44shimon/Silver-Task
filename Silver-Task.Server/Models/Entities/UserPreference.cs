namespace Silver_Task.Server.Models.Entities
{
    /// <summary>One row per user (lazily created on first access — see
    /// UserPreferencesService.GetOrCreateAsync), not an EAV table, since this is a small fixed
    /// set of fields defined by the product spec rather than something that needs to grow
    /// without a migration (unlike UserNotificationSetting).</summary>
    public class UserPreference
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Theme { get; set; } = "System";

        public Guid? DefaultProjectId { get; set; }

        /// <summary>One of the five project view ids ("table"/"kanban"/"calendar"/"timeline"/
        /// "gantt"), or null for "no preference — use Table". Kept as free text (not an enum)
        /// so it stays a plain string match against ProjectViewTabs' own ViewId union, the same
        /// value already carried in the ?view= URL param.</summary>
        public string? DefaultTaskView { get; set; }

        public string DateFormat { get; set; } = "MM/dd/yyyy";

        public string TimeFormat { get; set; } = "12h";

        public string TimeZone { get; set; } = "UTC";

        public int ItemsPerPage { get; set; } = 25;

        /// <summary>Phase 36 — "Immediately" (default) means each eligible notification emails as
        /// it happens (the existing, pre-Phase-36 behavior); "Daily" batches non-urgent email into
        /// one digest and suppresses the individual emails that would otherwise fire (Urgent-
        /// priority notifications, e.g. TaskOverdue, still send immediately regardless — see
        /// NotificationService's own doc comment); "Never" sends no notification email at all.
        /// Purely an email-channel setting — in-app notifications are unaffected either way.</summary>
        public string DigestFrequency { get; set; } = "Immediately";

        public bool QuietHoursEnabled { get; set; }

        /// <summary>Interpreted in this user's own TimeZone (above), not UTC — see
        /// NotificationService.IsWithinQuietHours. Suppresses *email* only; in-app notifications
        /// are always stored regardless (per the spec's own "do not lose notifications" rule).</summary>
        public TimeOnly? QuietHoursStart { get; set; }

        public TimeOnly? QuietHoursEnd { get; set; }

        /// <summary>Bookkeeping for NotificationDigestBackgroundService — the last time this user
        /// was sent a daily digest, so a user's digest is never sent twice in the same local day
        /// no matter how often the sweep ticks.</summary>
        public DateTime? LastDigestSentAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }

        public Project? DefaultProject { get; set; }
    }
}
