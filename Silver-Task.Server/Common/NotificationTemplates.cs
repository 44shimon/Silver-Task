using System.Net;

namespace Silver_Task.Server.Common
{
    /// <summary>Centralized HTML email rendering (Phase 36's "keep templates centralized"
    /// requirement) — the one place that turns a notification's Title/Message (or a digest's
    /// computed counts) into an actual email body, applying the Silver Task branding consistently
    /// and, critically, HTML-encoding every piece of user-generated text before it goes into the
    /// markup. Title/Message ultimately originate from things like task titles, comment text, and
    /// automation names — all user-controlled — so encoding here is what stands between "notify
    /// me about task X" and a stored-XSS email (see the spec's own "Template Security" section).
    /// ActionUrl is a same-origin relative path (see Notification.ActionUrl's own doc comment),
    /// resolved against the configured app base URL only for the email's absolute link — never
    /// itself user-controlled, so it doesn't need the same encoding treatment, just concatenation.</summary>
    public static class NotificationTemplates
    {
        public static (string Subject, string HtmlBody) ForNotification(string title, string message, string? actionUrl, string appBaseUrl, string appName)
        {
            var safeTitle = WebUtility.HtmlEncode(title);
            var safeMessage = WebUtility.HtmlEncode(message);
            var safeAppName = WebUtility.HtmlEncode(appName);

            var button = actionUrl is null
                ? ""
                : $"""<p style="margin-top:24px"><a href="{WebUtility.HtmlEncode(appBaseUrl.TrimEnd('/') + actionUrl)}" style="background:#4f46e5;color:#ffffff;padding:10px 18px;border-radius:6px;text-decoration:none;font-weight:600">Open in {safeAppName}</a></p>""";

            var html = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;border:1px solid #e5e7eb;border-radius:8px">
                  <p style="font-size:13px;color:#6b7280;text-transform:uppercase;letter-spacing:0.04em;margin:0 0 16px">{safeAppName}</p>
                  <h2 style="margin:0 0 12px;font-size:18px;color:#111827">{safeTitle}</h2>
                  <p style="margin:0;color:#374151;font-size:14px;line-height:1.5">{safeMessage}</p>
                  {button}
                </div>
                """;

            return (title, html);
        }

        public static (string Subject, string HtmlBody) ForDigest(
            string appName, string appBaseUrl, int assignedCount, int dueTodayCount, int overdueCount, int newMentionsCount, int newCommentsCount)
        {
            var safeAppName = WebUtility.HtmlEncode(appName);
            var subject = $"{appName} — Daily Summary";

            var html = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;border:1px solid #e5e7eb;border-radius:8px">
                  <p style="font-size:13px;color:#6b7280;text-transform:uppercase;letter-spacing:0.04em;margin:0 0 16px">{safeAppName}</p>
                  <h2 style="margin:0 0 16px;font-size:18px;color:#111827">Daily Summary</h2>
                  <table style="width:100%;font-size:14px;color:#374151;border-collapse:collapse">
                    <tr><td style="padding:4px 0">Tasks assigned to you</td><td style="text-align:right;font-weight:600">{assignedCount}</td></tr>
                    <tr><td style="padding:4px 0">Due today</td><td style="text-align:right;font-weight:600">{dueTodayCount}</td></tr>
                    <tr><td style="padding:4px 0">Overdue</td><td style="text-align:right;font-weight:600;color:#dc2626">{overdueCount}</td></tr>
                    <tr><td style="padding:4px 0">New mentions</td><td style="text-align:right;font-weight:600">{newMentionsCount}</td></tr>
                    <tr><td style="padding:4px 0">New comments</td><td style="text-align:right;font-weight:600">{newCommentsCount}</td></tr>
                  </table>
                  <p style="margin-top:24px"><a href="{WebUtility.HtmlEncode(appBaseUrl.TrimEnd('/') + "/notifications")}" style="background:#4f46e5;color:#ffffff;padding:10px 18px;border-radius:6px;text-decoration:none;font-weight:600">View Notifications</a></p>
                </div>
                """;

            return (subject, html);
        }
    }
}
