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
            builder.Property(a => a.Description).HasMaxLength(2000);

            builder.Property(a => a.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(a => a.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(a => a.ProjectId);
            builder.HasIndex(a => a.TaskId);
            builder.HasIndex(a => a.CommentId);
            builder.HasIndex(a => a.CreatedAt);
            // Every list query filters on IsDeleted first — see AttachmentService — so this
            // composite (not IsDeleted alone) is what actually serves those queries efficiently.
            builder.HasIndex(a => new { a.TaskId, a.IsDeleted });
            builder.HasIndex(a => new { a.ProjectId, a.IsDeleted });
            // Serves "files in this folder" (Phase 34), the Project Files browser's primary query
            // once folder navigation is in play.
            builder.HasIndex(a => new { a.FolderId, a.IsDeleted });
            builder.HasIndex(a => a.CategoryId);

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

            // SetNull — deleting a folder never implicitly destroys the files that were in it;
            // FolderService always explicitly moves-or-soft-deletes an attachment's FolderId
            // before removing the folder row, so in practice this constraint is a backstop, same
            // reasoning as TaskItem.ParentTask's own SetNull/Restrict choices.
            builder.HasOne(a => a.Folder)
                .WithMany()
                .HasForeignKey(a => a.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            // SetNull — deactivating/deleting a FileCategory (when unused) must never cascade
            // into deleting the files that referenced it; a used category can't be hard-deleted
            // at all (see FileCategoryService.DeleteAsync), so this only ever fires for an
            // already-unused category, where it's a no-op in practice.
            builder.HasOne(a => a.Category)
                .WithMany()
                .HasForeignKey(a => a.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
