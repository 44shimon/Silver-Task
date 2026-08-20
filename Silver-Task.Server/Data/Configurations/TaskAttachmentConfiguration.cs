using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
    {
        public void Configure(EntityTypeBuilder<TaskAttachment> builder)
        {
            builder.ToTable("TaskAttachments");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
            builder.Property(a => a.MimeType).IsRequired().HasMaxLength(200);
            builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);

            builder.Property(a => a.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(a => a.TaskId);

            builder.HasOne(a => a.Task)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
