using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskActivityConfiguration : IEntityTypeConfiguration<TaskActivity>
    {
        public void Configure(EntityTypeBuilder<TaskActivity> builder)
        {
            builder.ToTable("TaskActivities");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
            builder.Property(a => a.FieldName).HasMaxLength(100);
            builder.Property(a => a.OldValue).HasColumnType("text");
            builder.Property(a => a.NewValue).HasColumnType("text");

            builder.Property(a => a.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(a => a.TaskId);
            builder.HasIndex(a => a.CreatedAt);

            builder.HasOne(a => a.Task)
                .WithMany(t => t.Activities)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // SetNull: audit history must survive even if the acting user is later deleted.
            builder.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
