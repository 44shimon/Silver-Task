using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskCustomValueConfiguration : IEntityTypeConfiguration<TaskCustomValue>
    {
        public void Configure(EntityTypeBuilder<TaskCustomValue> builder)
        {
            builder.ToTable("TaskCustomValues");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.Value).HasColumnType("text");

            builder.Property(v => v.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(v => v.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(v => new { v.TaskId, v.CustomFieldId }).IsUnique();
            builder.HasIndex(v => v.CustomFieldId);

            builder.HasOne(v => v.Task)
                .WithMany(t => t.CustomValues)
                .HasForeignKey(v => v.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.CustomField)
                .WithMany(f => f.Values)
                .HasForeignKey(v => v.CustomFieldId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
