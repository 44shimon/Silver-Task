using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class TaskTemplateConfiguration : IEntityTypeConfiguration<TaskTemplate>
    {
        public void Configure(EntityTypeBuilder<TaskTemplate> builder)
        {
            builder.ToTable("TaskTemplates");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name).IsRequired().HasMaxLength(500);
            builder.Property(t => t.Description).HasMaxLength(10000);
            builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(t => t.AssignmentMode).IsRequired().HasMaxLength(20);

            builder.Property(t => t.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
            builder.Property(t => t.UpdatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(t => t.CreatedByUserId);
            builder.HasIndex(t => t.IsArchived);

            builder.HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
