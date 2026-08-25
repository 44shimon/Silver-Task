using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
    {
        public void Configure(EntityTypeBuilder<TaskItem> builder)
        {
            builder.ToTable("Tasks");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
            builder.Property(t => t.Description).HasColumnType("text");

            builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(t => t.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(t => t.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Composite index covers "ordered task list for a project", the primary spreadsheet query.
            builder.HasIndex(t => new { t.ProjectId, t.SortOrder });
            builder.HasIndex(t => t.AssignedToUserId);
            builder.HasIndex(t => t.Status);
            builder.HasIndex(t => t.Priority);
            builder.HasIndex(t => t.DueDate);
            // Serves "ordered siblings under a parent" (Phase 30) — the composite index above
            // doesn't help that query shape since it's keyed on ProjectId, not ParentTaskId.
            builder.HasIndex(t => new { t.ParentTaskId, t.SortOrder });

            builder.HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Restrict, deliberately — a parent with existing children can never be deleted by a
            // bare DB-level cascade (which would silently wipe an entire subtask tree). TaskService
            // always explicitly reparents-or-cascades children itself *before* removing the parent
            // row, so this constraint should never actually fire in normal use; it exists as a
            // backstop against ever accidentally shipping a code path that doesn't.
            builder.HasOne(t => t.ParentTask)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
