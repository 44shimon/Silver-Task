using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class SavedViewShareConfiguration : IEntityTypeConfiguration<SavedViewShare>
    {
        public void Configure(EntityTypeBuilder<SavedViewShare> builder)
        {
            builder.ToTable("SavedViewShares");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(s => new { s.SavedViewId, s.SharedWithUserId }).IsUnique();
            builder.HasIndex(s => s.SharedWithUserId);

            builder.HasOne(s => s.SavedView)
                .WithMany(v => v.Shares)
                .HasForeignKey(s => s.SavedViewId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.SharedWithUser)
                .WithMany()
                .HasForeignKey(s => s.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
