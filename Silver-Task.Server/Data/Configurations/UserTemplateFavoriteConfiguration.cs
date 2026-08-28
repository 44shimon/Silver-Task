using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserTemplateFavoriteConfiguration : IEntityTypeConfiguration<UserTemplateFavorite>
    {
        public void Configure(EntityTypeBuilder<UserTemplateFavorite> builder)
        {
            builder.ToTable("UserTemplateFavorites", t => t.HasCheckConstraint(
                "CK_UserTemplateFavorites_ExactlyOneParent",
                "(CASE WHEN \"ProjectTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) + " +
                "(CASE WHEN \"TaskTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) = 1"));
            builder.HasKey(f => f.Id);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(f => new { f.UserId, f.ProjectTemplateId }).IsUnique();
            builder.HasIndex(f => new { f.UserId, f.TaskTemplateId }).IsUnique();

            builder.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.ProjectTemplate)
                .WithMany(t => t.FavoritedBy)
                .HasForeignKey(f => f.ProjectTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.TaskTemplate)
                .WithMany(t => t.FavoritedBy)
                .HasForeignKey(f => f.TaskTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
