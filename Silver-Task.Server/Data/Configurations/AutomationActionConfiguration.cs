using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class AutomationActionConfiguration : IEntityTypeConfiguration<AutomationAction>
    {
        public void Configure(EntityTypeBuilder<AutomationAction> builder)
        {
            builder.ToTable("AutomationActions");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.ActionType).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(a => a.ParametersJson).IsRequired().HasColumnType("text");

            builder.HasIndex(a => a.AutomationId);

            builder.HasOne(a => a.Automation)
                .WithMany(a => a.Actions)
                .HasForeignKey(a => a.AutomationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
