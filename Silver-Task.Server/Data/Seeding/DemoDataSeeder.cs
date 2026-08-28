using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Data.Seeding
{
    /// <summary>
    /// Ad-hoc demo-data population, run via `dotnet run -- --seed` (see Program.cs — gated to
    /// Development, never runs as part of normal startup). Idempotent by unique key (email for
    /// users, name for projects) so it's safe to run against a database that already has real
    /// data from manual testing — it only ever adds rows for keys that don't already exist, never
    /// modifies or deletes anything found. Reuses the real IPasswordHasher (so seeded accounts can
    /// actually log in) and the real INotificationService (so seeded notifications go through the
    /// same preference/dedup/self-exclusion rules as organically-created ones) instead of
    /// hand-rolling parallel logic.
    /// </summary>
    public static class DemoDataSeeder
    {
        private const string SeedPassword = "Demo1234!";

        public static async Task RunAsync(IServiceProvider rootServices)
        {
            using var scope = rootServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            Console.WriteLine("=== Silver-Task demo data seeder ===");
            Console.WriteLine($"Before: {await db.Users.CountAsync()} users, {await db.Projects.CountAsync()} projects, {await db.Tasks.CountAsync()} tasks, {await db.Notifications.CountAsync()} notifications.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTime.UtcNow;

            var users = await EnsureUsersAsync(db, passwordHasher, now);
            var projects = await EnsureProjectsAsync(db, users, now);
            var newMembers = await EnsureMembershipsAsync(db, projects, users, now);
            var fields = await EnsureCustomFieldsAsync(db, projects);
            var tasks = await EnsureTasksAsync(db, projects, users, today, now);
            var comments = await EnsureCommentsAsync(db, tasks, users, now);

            await db.SaveChangesAsync();
            Console.WriteLine($"Inserted rows saved. Generating notifications for new activity...");

            // Real notification-generation logic for the events this run actually created, so
            // preferences/self-exclusion/dedup all apply exactly like they would for real usage.
            foreach (var (member, project, actorId) in newMembers)
            {
                await notificationService.NotifyAsync(
                    member.UserId, actorId, NotificationTypes.UserAddedToProject, "Added to project",
                    $"You were added to \"{project.Name}\".", null, project.Id);
            }

            foreach (var task in tasks.Where(t => t.IsNew && t.Entity.AssignedToUserId is not null))
            {
                await notificationService.NotifyAsync(
                    task.Entity.AssignedToUserId!.Value, task.OwnerId, NotificationTypes.TaskAssigned,
                    "Task assigned to you",
                    $"\"{task.Entity.Title}\" was assigned to you in \"{task.ProjectName}\".",
                    task.Entity.Id, task.Entity.ProjectId);

                if (task.Entity.Status == TaskItemStatus.Complete && task.Entity.AssignedToUserId != task.OwnerId)
                {
                    await notificationService.NotifyAsync(
                        task.OwnerId, task.Entity.AssignedToUserId, NotificationTypes.ProjectTaskCompleted, "Task completed",
                        $"\"{task.Entity.Title}\" was marked complete in \"{task.ProjectName}\".",
                        task.Entity.Id, task.Entity.ProjectId);
                }
            }

            foreach (var comment in comments)
            {
                if (comment.AssigneeId is Guid assigneeId && assigneeId != comment.AuthorId)
                {
                    await notificationService.NotifyAsync(
                        assigneeId, comment.AuthorId, NotificationTypes.CommentAdded, "New comment on your task",
                        $"{comment.AuthorName} commented on \"{comment.TaskTitle}\".", comment.TaskId, comment.ProjectId);
                }
                foreach (var mentionedId in comment.MentionedUserIds)
                {
                    await notificationService.NotifyAsync(
                        mentionedId, comment.AuthorId, NotificationTypes.MentionedInComment, "You were mentioned in a comment",
                        $"{comment.AuthorName} mentioned you in a comment on \"{comment.TaskTitle}\".", comment.TaskId, comment.ProjectId);
                }
            }

            // Real due-soon/overdue sweep — picks up every seeded task whose due date qualifies,
            // deduplicated exactly like the production background service's ticks.
            await notificationService.CreateDueSoonAndOverdueNotificationsAsync();

            Console.WriteLine($"After: {await db.Users.CountAsync()} users, {await db.Projects.CountAsync()} projects, {await db.Tasks.CountAsync()} tasks, {await db.Notifications.CountAsync()} notifications.");
            Console.WriteLine($"Seeded account password (for any newly-created user above): {SeedPassword}");
            Console.WriteLine("=== Done ===");
        }

        private static async Task<Dictionary<string, User>> EnsureUsersAsync(AppDbContext db, IPasswordHasher<User> hasher, DateTime now)
        {
            var specs = new (string Email, string Name, UserRole Role, bool IsActive, bool IsDeleted)[]
            {
                ("admin@example.com", "Alex Admin", UserRole.Administrator, true, false),
                ("sarah.manager@example.com", "Sarah Bennett", UserRole.Manager, true, false),
                ("james.manager@example.com", "James Carter", UserRole.Manager, true, false),
                ("alice@example.com", "Alice Novak", UserRole.Member, true, false),
                ("bob@example.com", "Bob Kim", UserRole.Member, true, false),
                ("carol@example.com", "Carol Diaz", UserRole.Member, true, false),
                // Deliberately included so the deactivated/deleted-user paths (inactive assignee
                // display, blocked login, excluded-from-new-assignment dropdown, etc.) have real
                // data to exercise instead of only being reachable by manually deactivating someone.
                ("dave.inactive@example.com", "Dave Holt", UserRole.Member, false, false),
                ("erin.deleted@example.com", "Erin Walsh", UserRole.Member, false, true),
            };

            var existing = await db.Users.Where(u => specs.Select(s => s.Email).Contains(u.Email)).ToDictionaryAsync(u => u.Email);
            var result = new Dictionary<string, User>();

            foreach (var spec in specs)
            {
                if (existing.TryGetValue(spec.Email, out var user))
                {
                    result[spec.Email] = user;
                    continue;
                }

                user = new User
                {
                    Id = Guid.NewGuid(),
                    Name = spec.Name,
                    Email = spec.Email,
                    PasswordHash = string.Empty,
                    Role = spec.Role,
                    IsActive = spec.IsActive,
                    IsDeleted = spec.IsDeleted,
                    CreatedAt = now.AddDays(-60)
                };
                user.PasswordHash = hasher.HashPassword(user, SeedPassword);
                if (spec.IsDeleted)
                {
                    user.DeletedAt = now.AddDays(-10);
                }

                db.Users.Add(user);
                result[spec.Email] = user;
            }

            return result;
        }

        private static async Task<Dictionary<string, Project>> EnsureProjectsAsync(AppDbContext db, Dictionary<string, User> users, DateTime now)
        {
            var specs = new (string Name, string Description, string OwnerEmail)[]
            {
                ("Property Renovation", "Full renovation of the downtown property, permits through final walkthrough.", "sarah.manager@example.com"),
                ("Office Buildout", "Build out the new office space on the 3rd floor.", "james.manager@example.com"),
                ("Marketing Website Relaunch", "Redesign and relaunch the public marketing site.", "admin@example.com"),
            };

            var existing = await db.Projects.Where(p => specs.Select(s => s.Name).Contains(p.Name)).ToDictionaryAsync(p => p.Name);
            var result = new Dictionary<string, Project>();

            foreach (var spec in specs)
            {
                if (existing.TryGetValue(spec.Name, out var project))
                {
                    result[spec.Name] = project;
                    continue;
                }

                var owner = users[spec.OwnerEmail];
                project = new Project
                {
                    Id = Guid.NewGuid(),
                    Name = spec.Name,
                    Description = spec.Description,
                    OwnerId = owner.Id,
                    CreatedAt = now.AddDays(-45)
                };
                db.Projects.Add(project);

                // The owner is always implicitly a member too (same rule ProjectService.CreateAsync follows).
                db.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project.Id, UserId = owner.Id, CreatedAt = now.AddDays(-45) });

                result[spec.Name] = project;
            }

            return result;
        }

        private static async Task<List<(ProjectMember Entity, Project Project, Guid ActorId)>> EnsureMembershipsAsync(
            AppDbContext db, Dictionary<string, Project> projects, Dictionary<string, User> users, DateTime now)
        {
            var specs = new (string ProjectName, string[] MemberEmails)[]
            {
                ("Property Renovation", ["alice@example.com", "bob@example.com", "dave.inactive@example.com"]),
                ("Office Buildout", ["alice@example.com", "carol@example.com"]),
                ("Marketing Website Relaunch", ["bob@example.com", "carol@example.com", "james.manager@example.com"]),
            };

            var added = new List<(ProjectMember, Project, Guid)>();

            foreach (var spec in specs)
            {
                var project = projects[spec.ProjectName];
                var existingMemberIds = await db.ProjectMembers
                    .Where(m => m.ProjectId == project.Id)
                    .Select(m => m.UserId)
                    .ToListAsync();

                foreach (var email in spec.MemberEmails)
                {
                    var user = users[email];
                    if (existingMemberIds.Contains(user.Id))
                    {
                        continue;
                    }

                    var member = new ProjectMember { Id = Guid.NewGuid(), ProjectId = project.Id, UserId = user.Id, CreatedAt = now.AddDays(-30) };
                    db.ProjectMembers.Add(member);
                    added.Add((member, project, project.OwnerId));
                }
            }

            return added;
        }

        private static async Task<Dictionary<string, CustomField>> EnsureCustomFieldsAsync(AppDbContext db, Dictionary<string, Project> projects)
        {
            var specs = new (string ProjectName, string FieldName, CustomFieldType Type, string[]? Options)[]
            {
                ("Property Renovation", "Budget", CustomFieldType.Currency, null),
                ("Property Renovation", "Contractor", CustomFieldType.Text, null),
                ("Office Buildout", "Vendor", CustomFieldType.Text, null),
                ("Office Buildout", "Approved", CustomFieldType.Checkbox, null),
                ("Marketing Website Relaunch", "Launch Phase", CustomFieldType.Dropdown, ["Discovery", "Design", "Build", "QA", "Launch"]),
            };

            var result = new Dictionary<string, CustomField>();

            foreach (var spec in specs)
            {
                var project = projects[spec.ProjectName];
                var key = $"{spec.ProjectName}:{spec.FieldName}";
                var field = await db.CustomFields.Include(f => f.Options)
                    .FirstOrDefaultAsync(f => f.ProjectId == project.Id && f.Name == spec.FieldName);

                if (field is null)
                {
                    field = new CustomField
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = project.Id,
                        Name = spec.FieldName,
                        Identifier = SlugifyFieldName(spec.FieldName),
                        FieldType = spec.Type,
                        SortOrder = result.Count(kv => kv.Key.StartsWith(spec.ProjectName + ":", StringComparison.Ordinal))
                    };
                    db.CustomFields.Add(field);

                    if (spec.Options is { Length: > 0 })
                    {
                        var sortOrder = 0;
                        foreach (var optionValue in spec.Options)
                        {
                            field.Options.Add(new CustomFieldOption { Id = Guid.NewGuid(), CustomFieldId = field.Id, Value = optionValue, SortOrder = sortOrder++ });
                        }
                    }
                }

                result[key] = field;
            }

            return result;
        }

        /// <summary>Seed-only slugifier — mirrors CustomFieldService's own Slugify but doesn't
        /// need the uniqueness-disambiguation loop, since every seeded field name here is
        /// already distinct within its project.</summary>
        private static string SlugifyFieldName(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');

        private record SeededTask(TaskItem Entity, bool IsNew, Guid OwnerId, string ProjectName);

        private static async Task<List<SeededTask>> EnsureTasksAsync(
            AppDbContext db, Dictionary<string, Project> projects, Dictionary<string, User> users, DateOnly today, DateTime now)
        {
            // (Project, Title, Status, Priority, AssigneeEmail?, DueDate offset in days from today?, CreatedAt offset in days)
            var specs = new (string ProjectName, string Title, TaskItemStatus Status, TaskPriority Priority, string? AssigneeEmail, int? DueOffset, int CreatedOffset)[]
            {
                ("Property Renovation", "Obtain building permit", TaskItemStatus.Complete, TaskPriority.High, "alice@example.com", -10, -40),
                ("Property Renovation", "Demo interior walls", TaskItemStatus.InProgress, TaskPriority.High, "bob@example.com", 1, -15),
                ("Property Renovation", "Electrical rough-in", TaskItemStatus.InProgress, TaskPriority.Medium, "bob@example.com", -2, -12),
                ("Property Renovation", "Plumbing inspection", TaskItemStatus.Waiting, TaskPriority.Medium, "alice@example.com", 5, -10),
                ("Property Renovation", "Order kitchen cabinets", TaskItemStatus.NotStarted, TaskPriority.Medium, "sarah.manager@example.com", 14, -5),
                ("Property Renovation", "Install drywall", TaskItemStatus.Blocked, TaskPriority.High, "dave.inactive@example.com", -1, -8),
                ("Property Renovation", "Paint exterior trim", TaskItemStatus.NotStarted, TaskPriority.Low, null, 21, -3),
                ("Property Renovation", "Final walkthrough", TaskItemStatus.NotStarted, TaskPriority.Urgent, "sarah.manager@example.com", 30, -2),

                ("Office Buildout", "Select furniture vendor", TaskItemStatus.Complete, TaskPriority.Medium, "carol@example.com", -5, -30),
                ("Office Buildout", "Network cabling install", TaskItemStatus.InProgress, TaskPriority.High, "alice@example.com", 0, -14),
                ("Office Buildout", "Order standing desks", TaskItemStatus.NotStarted, TaskPriority.Low, "james.manager@example.com", 10, -6),
                ("Office Buildout", "Security badge system", TaskItemStatus.Waiting, TaskPriority.Medium, null, 15, -6),
                ("Office Buildout", "Paint conference rooms", TaskItemStatus.InProgress, TaskPriority.Medium, "carol@example.com", -3, -9),
                ("Office Buildout", "Move-in day logistics", TaskItemStatus.NotStarted, TaskPriority.Urgent, "james.manager@example.com", 2, -1),

                ("Marketing Website Relaunch", "Competitor audit", TaskItemStatus.Complete, TaskPriority.Medium, "carol@example.com", -14, -35),
                ("Marketing Website Relaunch", "Wireframes v1", TaskItemStatus.Complete, TaskPriority.High, "bob@example.com", -7, -28),
                ("Marketing Website Relaunch", "Homepage design", TaskItemStatus.InProgress, TaskPriority.High, "bob@example.com", 3, -10),
                ("Marketing Website Relaunch", "Copywriting pass", TaskItemStatus.NotStarted, TaskPriority.Medium, "carol@example.com", 7, -5),
                ("Marketing Website Relaunch", "Dev: component library", TaskItemStatus.InProgress, TaskPriority.High, "james.manager@example.com", -1, -9),
                ("Marketing Website Relaunch", "QA regression pass", TaskItemStatus.NotStarted, TaskPriority.Medium, null, 20, -2),
                ("Marketing Website Relaunch", "Launch checklist", TaskItemStatus.NotStarted, TaskPriority.Urgent, "james.manager@example.com", 25, -1),
            };

            var result = new List<SeededTask>();
            var sortOrders = new Dictionary<Guid, double>();

            foreach (var spec in specs)
            {
                var project = projects[spec.ProjectName];
                var existingTask = await db.Tasks.FirstOrDefaultAsync(t => t.ProjectId == project.Id && t.Title == spec.Title);
                if (existingTask is not null)
                {
                    result.Add(new SeededTask(existingTask, false, project.OwnerId, spec.ProjectName));
                    continue;
                }

                sortOrders.TryGetValue(project.Id, out var nextSortOrder);
                sortOrders[project.Id] = nextSortOrder + 1;

                var assignee = spec.AssigneeEmail is null ? null : users[spec.AssigneeEmail];
                var createdAt = now.AddDays(spec.CreatedOffset);
                var task = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Title = spec.Title,
                    Status = spec.Status,
                    Priority = spec.Priority,
                    AssignedToUserId = assignee?.Id,
                    DueDate = spec.DueOffset is int offset ? today.AddDays(offset) : null,
                    CompletedAt = spec.Status == TaskItemStatus.Complete ? createdAt.AddDays(1) : null,
                    SortOrder = nextSortOrder,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                db.Tasks.Add(task);
                db.TaskActivities.Add(new TaskActivity
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = project.OwnerId,
                    Action = "Created",
                    CreatedAt = createdAt
                });

                result.Add(new SeededTask(task, true, project.OwnerId, spec.ProjectName));
            }

            return result;
        }

        private record SeededComment(Guid TaskId, Guid ProjectId, string TaskTitle, Guid AuthorId, string AuthorName, Guid? AssigneeId, IReadOnlyList<Guid> MentionedUserIds);

        private static async Task<List<SeededComment>> EnsureCommentsAsync(
            AppDbContext db, List<SeededTask> tasks, Dictionary<string, User> users, DateTime now)
        {
            var specs = new (string TaskTitle, string AuthorEmail, string Text, string[] MentionEmails)[]
            {
                ("Demo interior walls", "bob@example.com", "Started demo today, found some unexpected wiring. @Sarah Bennett can you take a look?", ["sarah.manager@example.com"]),
                ("Electrical rough-in", "alice@example.com", "This is now overdue, need an update on timeline.", []),
                ("Homepage design", "carol@example.com", "Loving the direction on this! @Bob Kim nice work on the mockups.", ["bob@example.com"]),
                ("Dev: component library", "admin@example.com", "Please prioritize this, it's overdue and blocking the design handoff.", []),
            };

            var result = new List<SeededComment>();

            foreach (var spec in specs)
            {
                var task = tasks.FirstOrDefault(t => t.Entity.Title == spec.TaskTitle);
                if (task is null)
                {
                    continue;
                }

                var alreadyExists = await db.TaskComments.AnyAsync(c => c.TaskId == task.Entity.Id && c.Text == spec.Text);
                if (alreadyExists)
                {
                    continue;
                }

                var author = users[spec.AuthorEmail];
                var comment = new TaskComment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Entity.Id,
                    UserId = author.Id,
                    Text = spec.Text,
                    CreatedAt = now.AddHours(-4),
                    UpdatedAt = now.AddHours(-4)
                };
                db.TaskComments.Add(comment);

                var mentionedIds = spec.MentionEmails.Select(email => users[email].Id).ToList();
                result.Add(new SeededComment(
                    task.Entity.Id, task.Entity.ProjectId, task.Entity.Title, author.Id, author.Name,
                    task.Entity.AssignedToUserId, mentionedIds));
            }

            return result;
        }
    }
}
