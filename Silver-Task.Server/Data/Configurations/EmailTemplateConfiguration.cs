using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
    {
        public void Configure(EntityTypeBuilder<EmailTemplate> builder)
        {
            builder.ToTable("EmailTemplates");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.NotificationType).IsRequired().HasMaxLength(50);
            builder.Property(t => t.SubjectTemplate).HasMaxLength(200);
            builder.Property(t => t.HeadingTemplate).HasMaxLength(200);
            builder.Property(t => t.BodyTemplate).HasMaxLength(2000);
            builder.Property(t => t.CtaText).HasMaxLength(60);
            builder.Property(t => t.FooterTemplate).HasMaxLength(500);

            builder.HasIndex(t => t.NotificationType).IsUnique();

            // Restrict, not SetNull/Cascade — deleting the admin who last edited a template
            // should never delete or silently reattribute the template itself (users are only
            // ever soft-deleted in this app anyway, see UserService.DeleteAsync).
            builder.HasOne(t => t.UpdatedByUser)
                .WithMany()
                .HasForeignKey(t => t.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
