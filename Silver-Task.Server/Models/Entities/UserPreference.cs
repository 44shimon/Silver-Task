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

        /// <summary>Phase 45 — the single master email on/off switch, checked before every
        /// per-type UserNotificationSetting.EmailEnabled check (see
        /// NotificationService.MaybeSendEmailAsync). Distinct from DigestFrequency == "Never"
        /// (which also suppresses all email) only in that this is a single obvious top-level
        /// toggle rather than a value buried in a frequency dropdown — the two are independent
        /// switches that both have to allow an email through. Never affects in-app notifications
        /// or account-critical email (there is no password-reset/security email path in this app
        /// that goes through NotifyAsync at all, so this switch has nothing unsafe to disable).</summary>
        public bool EmailNotificationsEnabled { get; set; } = true;

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

        /// <summary>Phase 37 — "Dashboard" (default), "MyTasks", or "LastVisited". Read by the
        /// frontend's landing-redirect at "/" (see routes/AppRoutes.tsx); "LastVisited" is
        /// resolved client-side from localStorage (a browser-local concept — see
        /// useLastVisitedPage's own doc comment on why that one deliberately isn't synced
        /// server-side), everything else is a fixed route this field names directly.</summary>
        public string DefaultLandingPage { get; set; } = "Dashboard";

        /// <summary>Phase 37 — small JSON blob (widget visibility + order), same "flexible shape
        /// that doesn't deserve its own EAV table" reasoning as Notification.Metadata: the known
        /// widget id set can grow without a migration, and nothing outside DashboardLayoutService
        /// ever needs to query *into* this column, so a free-form JSON string is simpler than
        /// modeling it relationally. Null means "use the default layout" (a brand-new user, or a
        /// user who has never customized their dashboard) — the frontend applies its own default
        /// widget list in that case, not this column.</summary>
        public string? DashboardLayout { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }

        public Project? DefaultProject { get; set; }
    }
}
