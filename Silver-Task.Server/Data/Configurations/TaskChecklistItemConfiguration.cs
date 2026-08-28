using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskChecklistItemConfiguration : IEntityTypeConfiguration<TaskChecklistItem>
    {
        public void Configure(EntityTypeBuilder<TaskChecklistItem> builder)
        {
            builder.ToTable("TaskChecklistItems");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Text).IsRequired().HasMaxLength(500);
            builder.Property(c => c.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(c => c.TaskId);

            builder.HasOne(c => c.Task)
                .WithMany(t => t.ChecklistItems)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
