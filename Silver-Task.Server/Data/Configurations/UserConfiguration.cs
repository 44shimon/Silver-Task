using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
            builder.Property(u => u.PasswordHash).IsRequired();

            builder.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(u => u.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(u => u.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(u => u.Email).IsUnique();

            // Self-referencing and nullable — restrict rather than cascade/set-null on the FK's
            // own delete behavior, since the admin who performed a deletion is never itself
            // hard-deleted (soft-delete only), so this path never actually fires in practice.
            builder.HasOne(u => u.DeletedByUser)
                .WithMany()
                .HasForeignKey(u => u.DeletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
