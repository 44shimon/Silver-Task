using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDelivery>
    {
        public void Configure(EntityTypeBuilder<EmailDelivery> builder)
        {
            builder.ToTable("EmailDeliveries");
            builder.HasKey(d => d.Id);

            builder.Property(d => d.RecipientEmail).IsRequired().HasMaxLength(320);
            builder.Property(d => d.NotificationType).IsRequired().HasMaxLength(50);
            builder.Property(d => d.Title).IsRequired().HasMaxLength(200);
            builder.Property(d => d.Message).IsRequired().HasMaxLength(1000);
            builder.Property(d => d.ActionUrl).HasMaxLength(500);
            builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(d => d.LastError).HasMaxLength(500);

            // The background worker's own polling query (IEmailDeliveryService.ClaimDueAsync).
            builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
            builder.HasIndex(d => d.RecipientUserId);

            // Best-effort cross-reference only — see the entity's own doc comment on why this
            // isn't the required, cascading relationship a "queue row for a notification" shape
            // would normally have.
            builder.HasOne(d => d.Notification)
                .WithMany()
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(d => d.RecipientUser)
                .WithMany()
                .HasForeignKey(d => d.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.ActorUser)
                .WithMany()
                .HasForeignKey(d => d.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
