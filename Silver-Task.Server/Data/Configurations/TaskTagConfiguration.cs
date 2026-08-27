using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskTagConfiguration : IEntityTypeConfiguration<TaskTag>
    {
        public void Configure(EntityTypeBuilder<TaskTag> builder)
        {
            builder.ToTable("TaskTags");
            builder.HasKey(tt => tt.Id);

            builder.Property(tt => tt.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Prevents a duplicate task-tag relationship at the database level, mirroring
            // FileTag's own unique composite index exactly.
            builder.HasIndex(tt => new { tt.TaskId, tt.TagId }).IsUnique();
            builder.HasIndex(tt => tt.TagId);

            builder.HasOne(tt => tt.Task)
                .WithMany(t => t.TaskTags)
                .HasForeignKey(tt => tt.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(tt => tt.Tag)
                .WithMany()
                .HasForeignKey(tt => tt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
