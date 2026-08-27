using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");
            builder.HasKey(n => n.Id);

            builder.Property(n => n.Type).IsRequired().HasMaxLength(50);
            builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
            builder.Property(n => n.Message).IsRequired().HasMaxLength(1000);
            builder.Property(n => n.Metadata).HasMaxLength(2000);
            builder.Property(n => n.ActionUrl).HasMaxLength(500);
            builder.Property(n => n.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(n => n.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Covers the two real query shapes: the notification feed (UserId + CreatedAt DESC,
            // optionally filtered by IsRead) and the unread-count check (UserId + IsRead).
            builder.HasIndex(n => new { n.UserId, n.CreatedAt });
            builder.HasIndex(n => new { n.UserId, n.IsRead });
            // Phase 36 — the notification center's type filter tab (Tasks/Projects/Files/...)
            // and the EventId dedup check both scope by UserId first, so a composite starting
            // with UserId serves both without a second, separate single-column Type index.
            builder.HasIndex(n => new { n.UserId, n.Type });
            // Filtered (not a full unique index) — EventId is null for the vast majority of
            // notifications (only opt-in callers pass one, see Notification.EventId's own doc
            // comment), and Postgres treats every NULL as distinct so a plain unique index would
            // still "work", but a partial index keeps it small and makes the dedup intent explicit.
            builder.HasIndex(n => new { n.UserId, n.Type, n.EventId })
                .IsUnique()
                .HasFilter("\"EventId\" IS NOT NULL");

            builder.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Every one of these is nullable and SetNull — a deleted task/project/comment/file
            // shouldn't destroy the notification's own historical record, just the ability to
            // click through to it (see Notification.ActionUrl's own doc comment: the destination
            // route re-enforces authorization regardless, so a stale link is never a security
            // concern, only a UX one already handled by SetNull degrading gracefully).
            builder.HasOne(n => n.Task)
                .WithMany()
                .HasForeignKey(n => n.TaskId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(n => n.Project)
                .WithMany()
                .HasForeignKey(n => n.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(n => n.Comment)
                .WithMany()
                .HasForeignKey(n => n.CommentId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(n => n.File)
                .WithMany()
                .HasForeignKey(n => n.FileId)
                .OnDelete(DeleteBehavior.SetNull);

            // Restrict (not SetNull) would be wrong here since Users are never hard-deleted in
            // this app (soft delete only — see UserService.DeleteAsync's own doc comment), but
            // SetNull is still the correct *declared* behavior for the rare/future case where a
            // User row genuinely is removed, matching how every other actor-style reference in
            // this app degrades (e.g. TaskActivity.UserId).
            builder.HasOne(n => n.ActorUser)
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
