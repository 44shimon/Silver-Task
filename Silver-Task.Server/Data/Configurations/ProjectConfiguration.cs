using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Projects");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Description).HasMaxLength(2000);

            builder.Property(p => p.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(p => p.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(p => p.OwnerId);
            builder.HasIndex(p => p.IsArchived);

            // Restrict: ownership must be transferred before a user can be removed.
            builder.HasOne(p => p.Owner)
                .WithMany(u => u.OwnedProjects)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Phase 40 — SetNull: deleting the source template must never affect a project
            // already created from it (see ProjectTemplate's own doc comment).
            builder.HasOne(p => p.SourceProjectTemplate)
                .WithMany()
                .HasForeignKey(p => p.SourceProjectTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
