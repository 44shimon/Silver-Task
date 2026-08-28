using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectTemplateTaskChecklistItemConfiguration : IEntityTypeConfiguration<ProjectTemplateTaskChecklistItem>
    {
        public void Configure(EntityTypeBuilder<ProjectTemplateTaskChecklistItem> builder)
        {
            builder.ToTable("ProjectTemplateTaskChecklistItems");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Text).IsRequired().HasMaxLength(500);

            builder.HasIndex(c => c.ProjectTemplateTaskId);

            builder.HasOne(c => c.ProjectTemplateTask)
                .WithMany(t => t.ChecklistItems)
                .HasForeignKey(c => c.ProjectTemplateTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
