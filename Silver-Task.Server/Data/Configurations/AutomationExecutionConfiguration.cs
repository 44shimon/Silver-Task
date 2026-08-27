using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class AutomationExecutionConfiguration : IEntityTypeConfiguration<AutomationExecution>
    {
        public void Configure(EntityTypeBuilder<AutomationExecution> builder)
        {
            builder.ToTable("AutomationExecutions");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
            builder.Property(e => e.ResultSummary).HasMaxLength(2000);

            // Serves the Automation Runs list (ordered by recency) and Admin/project dashboards —
            // both always filter or sort by AutomationId + StartedAt together.
            builder.HasIndex(e => new { e.AutomationId, e.StartedAt });
            builder.HasIndex(e => e.Status);
            builder.HasIndex(e => e.TriggerEventId);

            // Cascade at the DB level, but never actually reached by the normal "Delete
            // automation" action (a soft delete — see Automation.IsDeleted) — only by the rare
            // admin hard-delete-a-whole-project path, which already destroys every other
            // project-owned row too.
            builder.HasOne(e => e.Automation)
                .WithMany()
                .HasForeignKey(e => e.AutomationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
