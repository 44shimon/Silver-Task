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

            // Phase 39 — widened from (TaskId, DependsOnTaskId) to include DependencyType: the
            // same pair can now legitimately hold two different relationships at once (e.g. a
            // Finish-to-Start AND a Finish-to-Finish edge between the same two tasks), but never
            // the same (pair, type) twice.
            builder.HasIndex(d => new { d.TaskId, d.DependsOnTaskId, d.DependencyType }).IsUnique();
            // The unique index above already serves TaskId-first lookups; DependsOnTaskId needs
            // its own index for the reverse direction ("what depends on this task" — the
            // Blocking/dependents query and the cascade-on-delete/blocked-state aggregate).
            builder.HasIndex(d => d.DependsOnTaskId);

            // Defense-in-depth alongside the application-level check in
            // TaskDependencyService.CreateAsync — a self-dependency can never be stored even if a
            // future code path forgets the app-level guard.
            builder.ToTable(t => t.HasCheckConstraint("CK_TaskDependencies_NoSelfDependency", "\"TaskId\" != \"DependsOnTaskId\""));

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
