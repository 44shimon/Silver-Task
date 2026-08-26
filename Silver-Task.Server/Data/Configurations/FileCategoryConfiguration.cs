using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class FileCategoryConfiguration : IEntityTypeConfiguration<FileCategory>
    {
        public void Configure(EntityTypeBuilder<FileCategory> builder)
        {
            builder.ToTable("FileCategories");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Description).HasMaxLength(500);

            builder.Property(c => c.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(c => c.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(c => c.IsActive);
        }
    }
}
