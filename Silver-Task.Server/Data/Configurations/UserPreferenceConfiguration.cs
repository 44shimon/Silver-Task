using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
    {
        public void Configure(EntityTypeBuilder<UserPreference> builder)
        {
            builder.ToTable("UserPreferences");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Theme).IsRequired().HasMaxLength(20);
            builder.Property(p => p.DefaultTaskView).HasMaxLength(20);
            builder.Property(p => p.DateFormat).IsRequired().HasMaxLength(20);
            builder.Property(p => p.TimeFormat).IsRequired().HasMaxLength(10);
            builder.Property(p => p.TimeZone).IsRequired().HasMaxLength(100);
            builder.Property(p => p.DefaultLandingPage).IsRequired().HasMaxLength(20);
            builder.Property(p => p.DashboardLayout).HasColumnType("text");
            builder.Property(p => p.WeeklyDigestDay).IsRequired().HasMaxLength(10).HasDefaultValue("Monday");
            // Explicit DB-level defaults (not just the C# initializer, which migrations don't see)
            // so existing rows backfilled by these columns' own migration come back at the same
            // sensible default every new row gets, not a zeroed-out/incorrect value.
            builder.Property(p => p.EmailNotificationsEnabled).HasDefaultValue(true);
            builder.Property(p => p.DailyDigestTime).HasDefaultValue(new TimeOnly(8, 0));
            builder.Property(p => p.WeeklyDigestTime).HasDefaultValue(new TimeOnly(8, 0));

            builder.Property(p => p.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(p => p.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            // One preferences row per user.
            builder.HasIndex(p => p.UserId).IsUnique();

            builder.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // A deleted/archived project shouldn't block deleting the project itself — the
            // preference just stops pointing anywhere, same reasoning as Task.AssignedToUserId
            // being SetNull rather than Restrict.
            builder.HasOne(p => p.DefaultProject)
                .WithMany()
                .HasForeignKey(p => p.DefaultProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
