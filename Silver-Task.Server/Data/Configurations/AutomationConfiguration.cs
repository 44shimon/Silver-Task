using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class AutomationConfiguration : IEntityTypeConfiguration<Automation>
    {
        public void Configure(EntityTypeBuilder<Automation> builder)
        {
            builder.ToTable("Automations");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
            builder.Property(a => a.Description).HasMaxLength(2000);
            builder.Property(a => a.TriggerType).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(a => a.LastError).HasMaxLength(2000);

            builder.Property(a => a.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(a => a.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(a => a.ProjectId);
            builder.HasIndex(a => a.IsActive);
            // Serves "find active, non-deleted automations for this project+trigger" —
            // AutomationService's primary matching query, evaluated on nearly every domain event.
            builder.HasIndex(a => new { a.ProjectId, a.TriggerType, a.IsActive, a.IsDeleted });

            // Cascade — matches Attachment/Folder's own Project FK exactly. This only ever fires
            // via AdminController's rare permanent (hard) project delete, which already cascades
            // away every other project-owned row too; the normal in-app "Delete automation" action
            // is a soft delete (see IsDeleted above) and never touches this constraint at all.
            builder.HasOne(a => a.Project)
                .WithMany(p => p.Automations)
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.CreatedBy)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.DeletedByUser)
                .WithMany()
                .HasForeignKey(a => a.DeletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
