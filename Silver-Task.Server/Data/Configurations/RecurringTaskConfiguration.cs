using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class RecurringTaskConfiguration : IEntityTypeConfiguration<RecurringTask>
    {
        public void Configure(EntityTypeBuilder<RecurringTask> builder)
        {
            builder.ToTable("RecurringTasks");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Title).IsRequired().HasMaxLength(500);
            builder.Property(r => r.Description).HasColumnType("text");
            builder.Property(r => r.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(r => r.Frequency).HasConversion<string>().HasMaxLength(20).IsRequired();
            // DaysOfWeek intentionally stored as its native int bitmask, not HasConversion<string>()
            // — see RecurrenceDayOfWeek's doc comment (a fixed 7-value flags set, not an
            // extensible-values enum).

            builder.Property(r => r.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(r => r.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(r => r.ProjectId);

            // The background generator's entire due-rule scan is this one index: active rules
            // whose NextOccurrenceDate has entered the generation window.
            builder.HasIndex(r => new { r.IsActive, r.NextOccurrenceDate });

            builder.HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // SetNull, not Restrict — a recurring rule should keep generating (as a top-level
            // series) even if its designated parent task is later deleted, rather than blocking
            // that deletion or erroring at generation time.
            builder.HasOne(r => r.ParentTask)
                .WithMany()
                .HasForeignKey(r => r.ParentTaskId)
                .OnDelete(DeleteBehavior.SetNull);

            // SetNull — deleting the template task stops subtask-structure copying (a disclosed
            // limitation) but the series itself keeps generating top-level occurrences.
            builder.HasOne(r => r.TemplateTask)
                .WithMany()
                .HasForeignKey(r => r.TemplateTaskId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(r => r.AssignedToUser)
                .WithMany()
                .HasForeignKey(r => r.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
