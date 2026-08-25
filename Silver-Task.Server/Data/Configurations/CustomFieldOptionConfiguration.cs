using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class CustomFieldOptionConfiguration : IEntityTypeConfiguration<CustomFieldOption>
    {
        public void Configure(EntityTypeBuilder<CustomFieldOption> builder)
        {
            builder.ToTable("CustomFieldOptions");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Value).IsRequired().HasMaxLength(500);

            builder.Property(o => o.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Same reasoning as CustomFieldConfiguration.IsActive — an explicit default so
            // existing options don't get silently deactivated by the ADD COLUMN migration.
            builder.Property(o => o.IsActive).HasDefaultValue(true);

            builder.HasIndex(o => new { o.CustomFieldId, o.SortOrder });

            builder.HasOne(o => o.CustomField)
                .WithMany(f => f.Options)
                .HasForeignKey(o => o.CustomFieldId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
