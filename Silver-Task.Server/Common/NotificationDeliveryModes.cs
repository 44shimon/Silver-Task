namespace Silver_Task.Server.Common
{
    /// <summary>Phase 46 — the closed set of values UserNotificationSetting.EmailDeliveryMode can
    /// hold. A small fixed set (unlike NotificationTypes, which genuinely is open-ended), so a
    /// plain string-constant class rather than a C# enum only to match the free-text storage
    /// convention already used for that column and stay consistent with how DigestFrequency/
    /// DigestFrequency-like string preferences are validated elsewhere in this app (allow-list
    /// checked against `All`, not enum parsing).</summary>
    public static class NotificationDeliveryModes
    {
        public const string Immediately = "Immediately";
        public const string DailyDigest = "DailyDigest";
        public const string WeeklyDigest = "WeeklyDigest";
        public const string Off = "Off";

        public static readonly IReadOnlyList<string> All = [Immediately, DailyDigest, WeeklyDigest, Off];
    }
}
