using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.ToTable("SystemSettings");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Key).IsRequired().HasMaxLength(100);
            builder.Property(s => s.ValueType).IsRequired().HasMaxLength(20);
            builder.Property(s => s.Description).HasMaxLength(500);
            builder.Property(s => s.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(s => s.Key).IsUnique();

            // A deleted admin's settings changes should stay attributable to *someone having
            // existed*, not force-null the audit trail — Restrict here would block deleting a
            // user who ever touched settings, which is too strict; SetNull just loses the
            // "who" while keeping "what changed and when".
            builder.HasOne(s => s.UpdatedByUser)
                .WithMany()
                .HasForeignKey(s => s.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
