using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.ToTable("Attachments", t => t.HasCheckConstraint(
                "CK_Attachments_ExactlyOneParent",
                "(CASE WHEN \"ProjectId\" IS NOT NULL THEN 1 ELSE 0 END) + " +
                "(CASE WHEN \"TaskId\" IS NOT NULL THEN 1 ELSE 0 END) + " +
                "(CASE WHEN \"CommentId\" IS NOT NULL THEN 1 ELSE 0 END) = 1"));
            builder.HasKey(a => a.Id);

            builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
            builder.Property(a => a.MimeType).IsRequired().HasMaxLength(200);
            builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);
            builder.Property(a => a.FileHash).HasMaxLength(64);

            builder.Property(a => a.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(a => a.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(a => a.ProjectId);
            builder.HasIndex(a => a.TaskId);
            builder.HasIndex(a => a.CommentId);
            // Every list query filters on IsDeleted first — see AttachmentService — so this
            // composite (not IsDeleted alone) is what actually serves those queries efficiently.
            builder.HasIndex(a => new { a.TaskId, a.IsDeleted });
            builder.HasIndex(a => new { a.ProjectId, a.IsDeleted });

            builder.HasOne(a => a.Project)
                .WithMany(p => p.Attachments)
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.Task)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.Comment)
                .WithMany(c => c.Attachments)
                .HasForeignKey(a => a.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.DeletedByUser)
                .WithMany()
                .HasForeignKey(a => a.DeletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
