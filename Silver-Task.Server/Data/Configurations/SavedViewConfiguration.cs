using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
    {
        public void Configure(EntityTypeBuilder<SavedView> builder)
        {
            builder.ToTable("SavedViews");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
            builder.Property(v => v.Description).HasMaxLength(2000);
            builder.Property(v => v.EntityType).IsRequired().HasMaxLength(20);
            builder.Property(v => v.FilterJson).IsRequired();
            builder.Property(v => v.SortField).HasMaxLength(200);
            builder.Property(v => v.GroupByField).HasMaxLength(200);
            builder.Property(v => v.Layout).IsRequired().HasMaxLength(20);

            builder.Property(v => v.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(v => v.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(v => v.CreatedByUserId);

            builder.HasOne(v => v.CreatedBy)
                .WithMany()
                .HasForeignKey(v => v.CreatedByUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
