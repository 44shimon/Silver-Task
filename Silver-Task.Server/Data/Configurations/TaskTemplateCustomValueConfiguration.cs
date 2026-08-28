using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskTemplateCustomValueConfiguration : IEntityTypeConfiguration<TaskTemplateCustomValue>
    {
        public void Configure(EntityTypeBuilder<TaskTemplateCustomValue> builder)
        {
            builder.ToTable("TaskTemplateCustomValues");
            builder.HasKey(v => v.Id);

            builder.HasIndex(v => new { v.TaskTemplateId, v.CustomFieldId }).IsUnique();
            builder.HasIndex(v => v.CustomFieldId);

            builder.HasOne(v => v.TaskTemplate)
                .WithMany(t => t.CustomValues)
                .HasForeignKey(v => v.TaskTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.CustomField)
                .WithMany()
                .HasForeignKey(v => v.CustomFieldId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
