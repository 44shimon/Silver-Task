using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskNotificationMuteConfiguration : IEntityTypeConfiguration<TaskNotificationMute>
    {
        public void Configure(EntityTypeBuilder<TaskNotificationMute> builder)
        {
            builder.ToTable("TaskNotificationMutes");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(m => new { m.UserId, m.TaskId }).IsUnique();

            builder.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Task)
                .WithMany()
                .HasForeignKey(m => m.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
