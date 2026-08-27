using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
    {
        public void Configure(EntityTypeBuilder<TaskComment> builder)
        {
            builder.ToTable("TaskComments");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Text).IsRequired().HasColumnType("text");

            builder.Property(c => c.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(c => c.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(c => c.TaskId);

            builder.HasOne(c => c.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: users are soft-deleted (IsActive) rather than removed, so comment
            // authorship is never expected to be orphaned in practice.
            builder.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // SetNull — deleting the Automation that posted a comment must never delete the
            // comment itself (see the spec's own "prefer retaining execution history" guidance,
            // applied here to the trail an automation left in the comment feed too).
            builder.HasOne(c => c.Automation)
                .WithMany()
                .HasForeignKey(c => c.AutomationId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
