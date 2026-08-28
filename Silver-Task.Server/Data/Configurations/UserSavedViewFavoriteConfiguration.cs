using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserSavedViewFavoriteConfiguration : IEntityTypeConfiguration<UserSavedViewFavorite>
    {
        public void Configure(EntityTypeBuilder<UserSavedViewFavorite> builder)
        {
            builder.ToTable("UserSavedViewFavorites");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // No SavedViewId FK — see UserSavedViewFavorite's own doc comment (system-default
            // virtual views have no real row to reference).
            builder.HasIndex(f => new { f.UserId, f.SavedViewId }).IsUnique();

            builder.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
