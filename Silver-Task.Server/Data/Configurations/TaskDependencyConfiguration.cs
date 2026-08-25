using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
    {
        public void Configure(EntityTypeBuilder<TaskDependency> builder)
        {
            builder.ToTable("TaskDependencies");
            builder.HasKey(d => d.Id);

            builder.Property(d => d.DependencyType).IsRequired().HasMaxLength(20);
            builder.Property(d => d.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Prevents "Task B depends on Task A" from ever being stored twice.
            builder.HasIndex(d => new { d.TaskId, d.DependsOnTaskId }).IsUnique();
            // The unique index above already serves TaskId-first lookups; DependsOnTaskId needs
            // its own index for the reverse direction ("what depends on this task" — the
            // Blocking/dependents query and the cascade-on-delete/blocked-state aggregate).
            builder.HasIndex(d => d.DependsOnTaskId);

            // Both FKs point at Tasks. Cascade on TaskId: deleting the dependent task removes its
            // own dependency rows. Cascade on DependsOnTaskId: deleting the prerequisite task
            // removes the dependency *relationship* (per spec — "remove the dependency
            // relationship, Task B should remain"), which is exactly what cascading a row out of
            // TaskDependencies does; it never touches the Tasks table itself, so Task B is
            // untouched. Npgsql (unlike SQL Server) allows multiple cascade paths into the same
            // table without complaint.
            builder.HasOne(d => d.Task)
                .WithMany()
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.DependsOnTask)
                .WithMany()
                .HasForeignKey(d => d.DependsOnTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
