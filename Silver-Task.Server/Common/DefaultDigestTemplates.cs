namespace Silver_Task.Server.Common
{
    /// <summary>Phase 46 — built-in copy for the two digest pseudo-types ("DailyDigest"/
    /// "WeeklyDigest"), plugged into the exact same EmailTemplate override table and admin
    /// editor/preview/reset UI Phase 45 built for per-notification templates (see
    /// DefaultEmailTemplates's own doc comment for the identical override-and-fallback pattern).
    /// These two are never in Common.NotificationTypes.All — they aren't real notification
    /// events, just the two extra keys the admin template screen also happens to manage.</summary>
    public static class DefaultDigestTemplates
    {
        public const string DailyDigestType = "DailyDigest";
        public const string WeeklyDigestType = "WeeklyDigest";

        public static readonly IReadOnlyDictionary<string, DefaultEmailTemplate> ByType = new Dictionary<string, DefaultEmailTemplate>
        {
            [DailyDigestType] = new(
                Subject: "Your Daily Summary — {{DigestDate}}",
                Heading: "Daily Summary",
                Body: "Good morning, {{UserName}}.\n\n{{DigestContent}}",
                CtaText: "Open Silver Task"),

            [WeeklyDigestType] = new(
                Subject: "Your Weekly Summary — {{DigestDate}}",
                Heading: "Weekly Summary",
                Body: "Hi {{UserName}}, here's what happened this week.\n\n{{DigestContent}}",
                CtaText: "Open Silver Task")
        };
    }
}
