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

        public DbSet<Attachment> Attachments => Set<Attachment>();

        public DbSet<CustomField> CustomFields => Set<CustomField>();

        public DbSet<CustomFieldOption> CustomFieldOptions => Set<CustomFieldOption>();

        public DbSet<TaskCustomValue> TaskCustomValues => Set<TaskCustomValue>();

        public DbSet<ProjectCustomValue> ProjectCustomValues => Set<ProjectCustomValue>();

        public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

        public DbSet<UserNotificationSetting> UserNotificationSettings => Set<UserNotificationSetting>();

        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

        public DbSet<Notification> Notifications => Set<Notification>();

        public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();

        public DbSet<RecurringTask> RecurringTasks => Set<RecurringTask>();

        public DbSet<RecurringTaskException> RecurringTaskExceptions => Set<RecurringTaskException>();

        public DbSet<Folder> Folders => Set<Folder>();

        public DbSet<FileCategory> FileCategories => Set<FileCategory>();

        public DbSet<Tag> Tags => Set<Tag>();

        public DbSet<FileTag> FileTags => Set<FileTag>();

        public DbSet<UserFileFavorite> UserFileFavorites => Set<UserFileFavorite>();

        public DbSet<TaskTag> TaskTags => Set<TaskTag>();

        public DbSet<Automation> Automations => Set<Automation>();

        public DbSet<AutomationCondition> AutomationConditions => Set<AutomationCondition>();

        public DbSet<AutomationAction> AutomationActions => Set<AutomationAction>();

        public DbSet<AutomationExecution> AutomationExecutions => Set<AutomationExecution>();

        public DbSet<SavedReport> SavedReports => Set<SavedReport>();

        public DbSet<SavedReportShare> SavedReportShares => Set<SavedReportShare>();

        public DbSet<UserReportFavorite> UserReportFavorites => Set<UserReportFavorite>();

        public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();

        public DbSet<ProjectTemplateTask> ProjectTemplateTasks => Set<ProjectTemplateTask>();

        public DbSet<ProjectTemplateTaskDependency> ProjectTemplateTaskDependencies => Set<ProjectTemplateTaskDependency>();

        public DbSet<ProjectTemplateTaskTag> ProjectTemplateTaskTags => Set<ProjectTemplateTaskTag>();

        public DbSet<ProjectTemplateTaskCustomValue> ProjectTemplateTaskCustomValues => Set<ProjectTemplateTaskCustomValue>();

        public DbSet<ProjectTemplateTaskChecklistItem> ProjectTemplateTaskChecklistItems => Set<ProjectTemplateTaskChecklistItem>();

        public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();

        public DbSet<TaskTemplateTag> TaskTemplateTags => Set<TaskTemplateTag>();

        public DbSet<TaskTemplateCustomValue> TaskTemplateCustomValues => Set<TaskTemplateCustomValue>();

        public DbSet<TaskTemplateChecklistItem> TaskTemplateChecklistItems => Set<TaskTemplateChecklistItem>();

        public DbSet<TemplateShare> TemplateShares => Set<TemplateShare>();

        public DbSet<UserTemplateFavorite> UserTemplateFavorites => Set<UserTemplateFavorite>();

        public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();

        public DbSet<SavedView> SavedViews => Set<SavedView>();

        public DbSet<SavedViewShare> SavedViewShares => Set<SavedViewShare>();

        public DbSet<UserSavedViewFavorite> UserSavedViewFavorites => Set<UserSavedViewFavorite>();

        public DbSet<TaskNotificationMute> TaskNotificationMutes => Set<TaskNotificationMute>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
