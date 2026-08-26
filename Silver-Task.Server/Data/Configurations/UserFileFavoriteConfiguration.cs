using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserFileFavoriteConfiguration : IEntityTypeConfiguration<UserFileFavorite>
    {
        public void Configure(EntityTypeBuilder<UserFileFavorite> builder)
        {
            builder.ToTable("UserFileFavorites");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Prevents a duplicate user-file favorite at the database level.
            builder.HasIndex(f => new { f.UserId, f.FileId }).IsUnique();
            builder.HasIndex(f => f.FileId);

            builder.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.File)
                .WithMany(a => a.FavoritedBy)
                .HasForeignKey(f => f.FileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
