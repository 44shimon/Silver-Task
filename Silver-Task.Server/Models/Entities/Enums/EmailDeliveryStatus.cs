namespace Silver_Task.Server.Models.Entities.Enums
{
    /// <summary>Phase 45 — lifecycle of one queued notification email. Queued is the initial
    /// state set by NotificationService.MaybeSendEmailAsync; everything past that is owned by
    /// EmailDeliveryService/EmailDeliveryBackgroundService. Sending is a short-lived transient
    /// state (marks a row as claimed by a worker tick so a slow send can't be picked up twice);
    /// Sent/Failed/Cancelled are all terminal.</summary>
    public enum EmailDeliveryStatus
    {
        Queued,
        Sending,
        Sent,
        Failed,
        Cancelled
    }
}
