using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectTemplateConfiguration : IEntityTypeConfiguration<ProjectTemplate>
    {
        public void Configure(EntityTypeBuilder<ProjectTemplate> builder)
        {
            builder.ToTable("ProjectTemplates");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
            builder.Property(t => t.Description).HasMaxLength(2000);

            builder.Property(t => t.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(t => t.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(t => t.CreatedByUserId);
            builder.HasIndex(t => t.IsArchived);

            builder.HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
