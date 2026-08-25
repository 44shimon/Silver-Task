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

            builder.Property(n => n.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Covers the two real query shapes: the notification feed (UserId + CreatedAt DESC,
            // optionally filtered by IsRead) and the unread-count check (UserId + IsRead).
            builder.HasIndex(n => new { n.UserId, n.CreatedAt });
            builder.HasIndex(n => new { n.UserId, n.IsRead });

            builder.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Both nullable, both SetNull — a deleted task/project shouldn't destroy the
            // notification's own historical record, just the ability to click through to it.
            builder.HasOne(n => n.Task)
                .WithMany()
                .HasForeignKey(n => n.TaskId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(n => n.Project)
                .WithMany()
                .HasForeignKey(n => n.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
