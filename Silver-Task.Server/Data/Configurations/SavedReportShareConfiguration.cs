using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class SavedReportShareConfiguration : IEntityTypeConfiguration<SavedReportShare>
    {
        public void Configure(EntityTypeBuilder<SavedReportShare> builder)
        {
            builder.ToTable("SavedReportShares");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Prevents sharing the same report with the same user twice.
            builder.HasIndex(s => new { s.SavedReportId, s.SharedWithUserId }).IsUnique();
            builder.HasIndex(s => s.SharedWithUserId);

            builder.HasOne(s => s.SavedReport)
                .WithMany(r => r.Shares)
                .HasForeignKey(s => s.SavedReportId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.SharedWithUser)
                .WithMany()
                .HasForeignKey(s => s.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
