using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data.Configurations
{
    public class ProjectTemplateTaskDependencyConfiguration : IEntityTypeConfiguration<ProjectTemplateTaskDependency>
    {
        public void Configure(EntityTypeBuilder<ProjectTemplateTaskDependency> builder)
        {
            builder.ToTable("ProjectTemplateTaskDependencies", t => t.HasCheckConstraint(
                "CK_ProjectTemplateTaskDependencies_NoSelfDependency",
                "\"TemplateTaskId\" != \"DependsOnTemplateTaskId\""));
            builder.HasKey(d => d.Id);

            builder.Property(d => d.DependencyType).IsRequired().HasMaxLength(20);
            builder.Property(d => d.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            builder.HasIndex(d => d.ProjectTemplateId);
            builder.HasIndex(d => new { d.TemplateTaskId, d.DependsOnTemplateTaskId, d.DependencyType }).IsUnique();
            builder.HasIndex(d => d.DependsOnTemplateTaskId);

            builder.HasOne(d => d.ProjectTemplate)
                .WithMany(pt => pt.Dependencies)
                .HasForeignKey(d => d.ProjectTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.TemplateTask)
                .WithMany()
                .HasForeignKey(d => d.TemplateTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.DependsOnTemplateTask)
                .WithMany()
                .HasForeignKey(d => d.DependsOnTemplateTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
