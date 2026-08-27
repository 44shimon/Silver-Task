using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserReportFavoriteConfiguration : IEntityTypeConfiguration<UserReportFavorite>
    {
        public void Configure(EntityTypeBuilder<UserReportFavorite> builder)
        {
            builder.ToTable("UserReportFavorites");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Prevents a duplicate user-report favorite at the database level.
            builder.HasIndex(f => new { f.UserId, f.SavedReportId }).IsUnique();
            builder.HasIndex(f => f.SavedReportId);

            builder.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.SavedReport)
                .WithMany(r => r.FavoritedBy)
                .HasForeignKey(f => f.SavedReportId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
