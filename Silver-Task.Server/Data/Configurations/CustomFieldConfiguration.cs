using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Data.Configurations
{
    public class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
    {
        public void Configure(EntityTypeBuilder<CustomField> builder)
        {
            builder.ToTable("CustomFields");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);

            builder.Property(f => f.Identifier).IsRequired().HasMaxLength(200);

            builder.Property(f => f.Description).HasMaxLength(1000);

            builder.Property(f => f.FieldType).HasConversion<string>().HasMaxLength(20).IsRequired();

            // Explicit default (not just the C# property initializer) for the same reason as
            // IsActive below — every field that existed before this phase's migration ran must
            // come out the other side as EntityType.Task, never a null/zero value.
            builder.Property(f => f.EntityType).HasConversion<string>().HasMaxLength(10).IsRequired().HasDefaultValue(CustomFieldEntityType.Task);

            builder.Property(f => f.DefaultValue).HasMaxLength(1000);

            // Explicit default, not just the C# property initializer — EF's migration generator
            // otherwise defaults an added bool column to false, which would deactivate every
            // existing custom field the moment this migration ran.
            builder.Property(f => f.IsActive).HasDefaultValue(true);

            builder.Property(f => f.GroupName).HasMaxLength(200);
            builder.Property(f => f.Placeholder).HasMaxLength(200);
            builder.Property(f => f.MinValue).HasColumnType("numeric");
            builder.Property(f => f.MaxValue).HasColumnType("numeric");
            builder.Property(f => f.IsPrivate).HasDefaultValue(false);
            builder.Property(f => f.VisibleToRoles).HasMaxLength(200);
            builder.Property(f => f.ConditionOperator).HasConversion<string>().HasMaxLength(25);
            builder.Property(f => f.ConditionValue).HasMaxLength(1000);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(f => f.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(f => new { f.EntityType, f.ProjectId, f.SortOrder });

            // ProjectId is optional (null = applies to every project, Administrator-only) —
            // the FK is nullable and Cascade only ever fires for project-scoped fields, since a
            // null-ProjectId row has nothing to cascade from.
            builder.HasOne(f => f.Project)
                .WithMany(p => p.CustomFields)
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing "controlled by" edge for conditional visibility — Restrict, not
            // Cascade, so deleting the controlling field can't cascade-delete every field that
            // merely references it (same reasoning as ProjectTemplateTask's own self-reference).
            builder.HasOne(f => f.ConditionField)
                .WithMany()
                .HasForeignKey(f => f.ConditionFieldId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
