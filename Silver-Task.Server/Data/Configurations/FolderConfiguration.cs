using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class FolderConfiguration : IEntityTypeConfiguration<Folder>
    {
        public void Configure(EntityTypeBuilder<Folder> builder)
        {
            builder.ToTable("Folders");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.Name).IsRequired().HasMaxLength(255);

            builder.Property(f => f.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(f => f.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(f => f.ProjectId);
            builder.HasIndex(f => f.ParentFolderId);
            // Serves "siblings under a parent, active only" — the primary query shape for both
            // folder-tree loading and the duplicate-name check (FolderService.EnsureNameIsAvailableAsync).
            builder.HasIndex(f => new { f.ProjectId, f.ParentFolderId, f.IsDeleted });

            builder.HasOne(f => f.Project)
                .WithMany(p => p.Folders)
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, deliberately — same reasoning as TaskItem.ParentTask (Phase 30):
            // FolderService always explicitly reparents-or-cascades children *before* removing a
            // folder row, so a bare DB-level cascade here would risk silently wiping an entire
            // subtree if that invariant were ever violated by a future code path. This constraint
            // is the backstop, not the primary mechanism.
            builder.HasOne(f => f.ParentFolder)
                .WithMany(f => f.Subfolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.CreatedBy)
                .WithMany()
                .HasForeignKey(f => f.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.DeletedByUser)
                .WithMany()
                .HasForeignKey(f => f.DeletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
