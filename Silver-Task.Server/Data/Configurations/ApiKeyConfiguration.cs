using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
    {
        public void Configure(EntityTypeBuilder<ApiKey> builder)
        {
            builder.ToTable("ApiKeys");
            builder.HasKey(k => k.Id);

            builder.Property(k => k.Name).IsRequired().HasMaxLength(200);
            builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(20);
            builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(64);

            builder.Property(k => k.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(k => k.KeyHash).IsUnique();
            builder.HasIndex(k => k.UserId);

            // Restrict: users are soft-deleted (IsActive) rather than removed, so a key's owner is
            // never expected to be orphaned in practice — same reasoning TaskCommentConfiguration
            // already applies to comment authorship.
            builder.HasOne(k => k.User)
                .WithMany()
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(k => k.RevokedByUser)
                .WithMany()
                .HasForeignKey(k => k.RevokedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(k => k.CreatedByUser)
                .WithMany()
                .HasForeignKey(k => k.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
