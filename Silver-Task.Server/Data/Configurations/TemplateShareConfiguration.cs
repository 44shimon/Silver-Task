using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TemplateShareConfiguration : IEntityTypeConfiguration<TemplateShare>
    {
        public void Configure(EntityTypeBuilder<TemplateShare> builder)
        {
            builder.ToTable("TemplateShares", t => t.HasCheckConstraint(
                "CK_TemplateShares_ExactlyOneParent",
                "(CASE WHEN \"ProjectTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) + " +
                "(CASE WHEN \"TaskTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) = 1"));
            builder.HasKey(s => s.Id);

            builder.Property(s => s.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(s => new { s.ProjectTemplateId, s.SharedWithUserId }).IsUnique();
            builder.HasIndex(s => new { s.TaskTemplateId, s.SharedWithUserId }).IsUnique();
            builder.HasIndex(s => s.SharedWithUserId);

            builder.HasOne(s => s.ProjectTemplate)
                .WithMany(t => t.Shares)
                .HasForeignKey(s => s.ProjectTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.TaskTemplate)
                .WithMany(t => t.Shares)
                .HasForeignKey(s => s.TaskTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.SharedWithUser)
                .WithMany()
                .HasForeignKey(s => s.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
