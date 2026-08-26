using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class RecurringTaskExceptionConfiguration : IEntityTypeConfiguration<RecurringTaskException>
    {
        public void Configure(EntityTypeBuilder<RecurringTaskException> builder)
        {
            builder.ToTable("RecurringTaskExceptions");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.ExceptionType).IsRequired().HasMaxLength(20);
            builder.Property(e => e.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // The actual duplicate/recreate-prevention guard — one exception row per skipped date.
            builder.HasIndex(e => new { e.RecurringTaskId, e.OccurrenceDate }).IsUnique();

            builder.HasOne(e => e.RecurringTask)
                .WithMany(r => r.Exceptions)
                .HasForeignKey(e => e.RecurringTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
