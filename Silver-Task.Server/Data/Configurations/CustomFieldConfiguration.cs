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

            builder.Property(f => f.Description).HasMaxLength(1000);

            builder.Property(f => f.FieldType).HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(f => f.DefaultValue).HasMaxLength(1000);

            // Explicit default, not just the C# property initializer — EF's migration generator
            // otherwise defaults an added bool column to false, which would deactivate every
            // existing custom field the moment this migration ran.
            builder.Property(f => f.IsActive).HasDefaultValue(true);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(f => f.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(f => new { f.ProjectId, f.SortOrder });

            // ProjectId is optional (null = applies to every project, Administrator-only) —
            // the FK is nullable and Cascade only ever fires for project-scoped fields, since a
            // null-ProjectId row has nothing to cascade from.
            builder.HasOne(f => f.Project)
                .WithMany(p => p.CustomFields)
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
