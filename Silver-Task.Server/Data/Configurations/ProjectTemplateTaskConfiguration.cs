using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectTemplateTaskConfiguration : IEntityTypeConfiguration<ProjectTemplateTask>
    {
        public void Configure(EntityTypeBuilder<ProjectTemplateTask> builder)
        {
            builder.ToTable("ProjectTemplateTasks");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
            builder.Property(t => t.Description).HasMaxLength(10000);
            builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(t => t.AssignmentMode).IsRequired().HasMaxLength(20);

            builder.Property(t => t.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(t => t.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(t => t.ProjectTemplateId);
            builder.HasIndex(t => t.ParentTemplateTaskId);

            builder.HasOne(t => t.ProjectTemplate)
                .WithMany(pt => pt.Tasks)
                .HasForeignKey(t => t.ProjectTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict on the self-reference — same reasoning as TaskItem.ParentTask: avoids a
            // multiple-cascade-path ambiguity, and the service layer explicitly collects/removes
            // a subtree together when a template task with children is deleted (mirrors
            // TaskService.DeleteAsync's own CollectDescendantsAsync pattern).
            builder.HasOne(t => t.ParentTemplateTask)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(t => t.ParentTemplateTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            // SetNull — a template referencing a user who's since been deactivated/removed
            // shouldn't block editing the template; AssignmentMode simply won't resolve to
            // anyone at instantiation time in that case (same "graceful degrade" precedent as
            // RecurringTask's own assignee handling).
            builder.HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
