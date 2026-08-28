using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskTemplateChecklistItemConfiguration : IEntityTypeConfiguration<TaskTemplateChecklistItem>
    {
        public void Configure(EntityTypeBuilder<TaskTemplateChecklistItem> builder)
        {
            builder.ToTable("TaskTemplateChecklistItems");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Text).IsRequired().HasMaxLength(500);

            builder.HasIndex(c => c.TaskTemplateId);

            builder.HasOne(c => c.TaskTemplate)
                .WithMany(t => t.ChecklistItems)
                .HasForeignKey(c => c.TaskTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
