using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
    {
        public void Configure(EntityTypeBuilder<CustomField> builder)
        {
            builder.ToTable("CustomFields");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);

            builder.Property(f => f.FieldType).HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(f => f.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(f => new { f.ProjectId, f.SortOrder });

            builder.HasOne(f => f.Project)
                .WithMany(p => p.CustomFields)
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
