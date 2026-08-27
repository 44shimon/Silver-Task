using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
    {
        public void Configure(EntityTypeBuilder<SavedReport> builder)
        {
            builder.ToTable("SavedReports");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
            builder.Property(r => r.Description).HasMaxLength(2000);
            builder.Property(r => r.Configuration).IsRequired();

            builder.Property(r => r.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(r => r.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(r => r.CreatedByUserId);
            builder.HasIndex(r => r.ProjectId);

            builder.HasOne(r => r.CreatedBy)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade matches Automation's own Project FK — only fires via the rare hard project
            // delete path (which already cascades away every other project-owned row); the config
            // is a small opaque JSON blob, never large task datasets, so this never risks the kind
            // of duplication the spec warns against for historical snapshots.
            builder.HasOne(r => r.Project)
                .WithMany(p => p.SavedReports)
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
