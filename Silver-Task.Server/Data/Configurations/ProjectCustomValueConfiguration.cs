using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectCustomValueConfiguration : IEntityTypeConfiguration<ProjectCustomValue>
    {
        public void Configure(EntityTypeBuilder<ProjectCustomValue> builder)
        {
            builder.ToTable("ProjectCustomValues");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.Value).HasColumnType("text");

            builder.Property(v => v.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(v => v.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(v => new { v.ProjectId, v.CustomFieldId }).IsUnique();
            builder.HasIndex(v => v.CustomFieldId);

            builder.HasOne(v => v.Project)
                .WithMany(p => p.CustomValues)
                .HasForeignKey(v => v.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.CustomField)
                .WithMany(f => f.ProjectValues)
                .HasForeignKey(v => v.CustomFieldId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
