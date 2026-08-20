using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();

        public DbSet<Project> Projects => Set<Project>();

        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        public DbSet<TaskComment> TaskComments => Set<TaskComment>();

        public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

        public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();

        public DbSet<CustomField> CustomFields => Set<CustomField>();

        public DbSet<CustomFieldOption> CustomFieldOptions => Set<CustomFieldOption>();

        public DbSet<TaskCustomValue> TaskCustomValues => Set<TaskCustomValue>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
