using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectTemplateTaskTagConfiguration : IEntityTypeConfiguration<ProjectTemplateTaskTag>
    {
        public void Configure(EntityTypeBuilder<ProjectTemplateTaskTag> builder)
        {
            builder.ToTable("ProjectTemplateTaskTags");
            builder.HasKey(t => t.Id);

            builder.HasIndex(t => new { t.ProjectTemplateTaskId, t.TagId }).IsUnique();
            builder.HasIndex(t => t.TagId);

            builder.HasOne(t => t.ProjectTemplateTask)
                .WithMany(pt => pt.Tags)
                .HasForeignKey(t => t.ProjectTemplateTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.Tag)
                .WithMany()
                .HasForeignKey(t => t.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
