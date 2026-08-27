using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class AutomationConditionConfiguration : IEntityTypeConfiguration<AutomationCondition>
    {
        public void Configure(EntityTypeBuilder<AutomationCondition> builder)
        {
            builder.ToTable("AutomationConditions");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Field).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Operator).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(c => c.Value).HasMaxLength(1000);

            builder.HasIndex(c => c.AutomationId);

            builder.HasOne(c => c.Automation)
                .WithMany(a => a.Conditions)
                .HasForeignKey(c => c.AutomationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
