using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskTemplateTagConfiguration : IEntityTypeConfiguration<TaskTemplateTag>
    {
        public void Configure(EntityTypeBuilder<TaskTemplateTag> builder)
        {
            builder.ToTable("TaskTemplateTags");
            builder.HasKey(t => t.Id);

            builder.HasIndex(t => new { t.TaskTemplateId, t.TagId }).IsUnique();
            builder.HasIndex(t => t.TagId);

            builder.HasOne(t => t.TaskTemplate)
                .WithMany(tt => tt.Tags)
                .HasForeignKey(t => t.TaskTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.Tag)
                .WithMany()
                .HasForeignKey(t => t.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
