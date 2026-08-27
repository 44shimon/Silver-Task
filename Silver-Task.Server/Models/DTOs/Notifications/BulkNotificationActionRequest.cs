using System.ComponentModel.DataAnnotations;

namespace Silver_Task.Server.Models.DTOs.Notifications
{
    /// <summary>Backs the notification center's "select several, then act" bulk operations
    /// (mark read, dismiss). Ids are always re-scoped to the caller's own notifications
    /// server-side (see NotificationService's own doc comment) — this list is never trusted as
    /// "these ids belong to the caller" on its own.</summary>
    public class BulkNotificationActionRequest
    {
        [Required, MinLength(1)]
        public required List<Guid> Ids { get; set; }
    }
}
