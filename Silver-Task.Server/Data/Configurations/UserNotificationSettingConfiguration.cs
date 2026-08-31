using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserNotificationSettingConfiguration : IEntityTypeConfiguration<UserNotificationSetting>
    {
        public void Configure(EntityTypeBuilder<UserNotificationSetting> builder)
        {
            builder.ToTable("UserNotificationSettings");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.NotificationType).IsRequired().HasMaxLength(50);
            builder.Property(s => s.EmailDeliveryMode).IsRequired().HasMaxLength(20);
            builder.Property(s => s.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(s => new { s.UserId, s.NotificationType }).IsUnique();

            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
