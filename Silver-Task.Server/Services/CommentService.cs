using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ICommentService
    {
        Task<IReadOnlyList<TaskComment>> GetAllForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole);

        Task<TaskComment> CreateAsync(Guid taskId, string text, Guid callerId, UserRole callerRole);

        /// <summary>Only the comment's author may edit or delete it — there is no admin/manager override (literal spec rule).</summary>
        Task<TaskComment> UpdateAsync(Guid commentId, string text, Guid callerId);

        Task DeleteAsync(Guid commentId, Guid callerId);
    }

    public class CommentService(AppDbContext db, IProjectAccessService projectAccess, ISystemSettingsService systemSettings) : ICommentService
    {
        private readonly AppDbContext _db = db;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;

        public async Task<IReadOnlyList<TaskComment>> GetAllForTaskAsync(Guid taskId, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            return await _db.TaskComments
                .Include(c => c.User)
                .Where(c => c.TaskId == taskId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<TaskComment> CreateAsync(Guid taskId, string text, Guid callerId, UserRole callerRole)
        {
            var task = await LoadTaskAsync(taskId);
            await _projectAccess.EnsureCanParticipateAsync(task.ProjectId, task.Project!.OwnerId, callerId, callerRole);

            // A blanket kill-switch — existing comments stay fully visible/readable either way,
            // this only gates *new* ones, checked after the normal participate-tier check so a
            // caller with no access to the task still gets the usual 403, not a misleading
            // "comments are disabled" for a task they couldn't see anyway.
            if (!await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowComments))
            {
                throw new ForbiddenException("Comments are currently disabled by an Administrator.");
            }

            var comment = new TaskComment
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = callerId,
                Text = text.Trim()
            };
            _db.TaskComments.Add(comment);
            await _db.SaveChangesAsync();

            comment.User = await _db.Users.FindAsync(callerId);
            return comment;
        }

        public async Task<TaskComment> UpdateAsync(Guid commentId, string text, Guid callerId)
        {
            var comment = await LoadCommentAsync(commentId);
            EnsureIsAuthor(comment, callerId);

            comment.Text = text.Trim();
            comment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return comment;
        }

        public async Task DeleteAsync(Guid commentId, Guid callerId)
        {
            var comment = await LoadCommentAsync(commentId);
            EnsureIsAuthor(comment, callerId);

            _db.TaskComments.Remove(comment);
            await _db.SaveChangesAsync();
        }

        private async Task<TaskItem> LoadTaskAsync(Guid taskId)
        {
            var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
            return task ?? throw new NotFoundException($"Task '{taskId}' was not found.");
        }

        private async Task<TaskComment> LoadCommentAsync(Guid commentId)
        {
            var comment = await _db.TaskComments.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == commentId);
            return comment ?? throw new NotFoundException($"Comment '{commentId}' was not found.");
        }

        private static void EnsureIsAuthor(TaskComment comment, Guid callerId)
        {
            if (comment.UserId != callerId)
            {
                throw new ForbiddenException("You can only edit or delete your own comments.");
            }
        }
    }
}
