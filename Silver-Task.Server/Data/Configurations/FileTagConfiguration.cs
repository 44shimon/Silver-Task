using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class FileTagConfiguration : IEntityTypeConfiguration<FileTag>
    {
        public void Configure(EntityTypeBuilder<FileTag> builder)
        {
            builder.ToTable("FileTags");
            builder.HasKey(ft => ft.Id);

            builder.Property(ft => ft.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // Prevents a duplicate file-tag relationship at the database level, not just in
            // application code — same "Id PK + unique composite index" shape as ProjectMember.
            builder.HasIndex(ft => new { ft.FileId, ft.TagId }).IsUnique();
            builder.HasIndex(ft => ft.TagId);

            builder.HasOne(ft => ft.File)
                .WithMany(a => a.FileTags)
                .HasForeignKey(ft => ft.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ft => ft.Tag)
                .WithMany()
                .HasForeignKey(ft => ft.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
