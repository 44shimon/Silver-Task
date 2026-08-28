using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectTemplateTaskCustomValueConfiguration : IEntityTypeConfiguration<ProjectTemplateTaskCustomValue>
    {
        public void Configure(EntityTypeBuilder<ProjectTemplateTaskCustomValue> builder)
        {
            builder.ToTable("ProjectTemplateTaskCustomValues");
            builder.HasKey(v => v.Id);

            builder.HasIndex(v => new { v.ProjectTemplateTaskId, v.CustomFieldId }).IsUnique();
            builder.HasIndex(v => v.CustomFieldId);

            builder.HasOne(v => v.ProjectTemplateTask)
                .WithMany(t => t.CustomValues)
                .HasForeignKey(v => v.ProjectTemplateTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.CustomField)
                .WithMany()
                .HasForeignKey(v => v.CustomFieldId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
