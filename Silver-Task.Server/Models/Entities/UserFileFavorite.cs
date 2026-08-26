namespace Silver_Task.Server.Models.Entities
{
    /// <summary>Per-user favorite marker (Phase 34) — deliberately a separate join row rather than
    /// an IsFavorite flag on Attachment, since favorite status is per-(user, file), not a property
    /// of the file itself.</summary>
    public class UserFileFavorite
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid FileId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public Attachment? File { get; set; }
    }
}
