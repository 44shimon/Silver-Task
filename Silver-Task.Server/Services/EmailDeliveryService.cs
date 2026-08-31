using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface IEmailDeliveryService
    {
        /// <summary>Claims (Status -> Sending) and returns up to <paramref name="batchSize"/>
        /// due rows — called once per EmailDeliveryBackgroundService tick. Claiming immediately
        /// (rather than leaving them Queued until sent) means a slow send in this tick can never
        /// be picked up again by the same single-worker loop's next tick.</summary>
        Task<IReadOnlyList<EmailDelivery>> ClaimDueAsync(int batchSize, CancellationToken cancellationToken);

        Task AttemptDeliveryAsync(EmailDelivery delivery, CancellationToken cancellationToken);

        /// <summary>Spec §31/§34 — a lightweight grouping for the case where several deliveries
        /// for the same (recipient, type) land in the same claimed batch (e.g. a bulk operation
        /// assigning many tasks to one user in quick succession): one combined email instead of
        /// several near-identical ones, instead of a full digest rebuild — see the class doc
        /// comment. Deliberately reveals no per-item entity details (just a count and a link to
        /// /notifications, which re-enforces authorization itself on load), so it needs no
        /// per-item access re-validation the way AttemptDeliveryAsync does.</summary>
        Task AttemptGroupedDeliveryAsync(IReadOnlyList<EmailDelivery> deliveries, CancellationToken cancellationToken);

        Task<(IReadOnlyList<EmailDelivery> Items, int TotalCount)> GetDeliveryLogAsync(int page, int pageSize);
    }

    /// <summary>
    /// Phase 45 — the background half of email delivery: NotificationService.MaybeSendEmailAsync
    /// only ever enqueues a self-contained EmailDelivery row; everything from "is this still
    /// safe/valid to send" through "actually send it" through "record the result" lives here,
    /// driven by EmailDeliveryBackgroundService. AttemptDeliveryAsync re-validates the
    /// recipient's access immediately before sending (not just at enqueue time) — a notification
    /// can sit queued for a little while, and access, the task, or the project can all change in
    /// that window (spec's own "re-check access before delivery" / "do not send protected
    /// information after access has been revoked" requirements).
    /// </summary>
    public class EmailDeliveryService(
        AppDbContext db,
        IEmailService emailService,
        IEmailTemplateService templateService,
        IProjectAccessService projectAccess,
        ISystemSettingsService systemSettings,
        IConfiguration configuration) : IEmailDeliveryService
    {
        // 2 attempts of backoff before giving up on the 3rd — spec's own "do not retry
        // indefinitely" bound.
        private static readonly TimeSpan[] RetryBackoff = [TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];
        private const int MaxAttempts = 3;

        private readonly AppDbContext _db = db;
        private readonly IEmailService _emailService = emailService;
        private readonly IEmailTemplateService _templateService = templateService;
        private readonly IProjectAccessService _projectAccess = projectAccess;
        private readonly ISystemSettingsService _systemSettings = systemSettings;
        private readonly IConfiguration _configuration = configuration;

        public async Task<IReadOnlyList<EmailDelivery>> ClaimDueAsync(int batchSize, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var due = await _db.EmailDeliveries
                .Include(d => d.ActorUser)
                .Where(d => d.Status == EmailDeliveryStatus.Queued && d.NextAttemptAt <= now)
                .OrderBy(d => d.QueuedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var delivery in due)
            {
                delivery.Status = EmailDeliveryStatus.Sending;
            }
            if (due.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            return due;
        }

        public async Task AttemptDeliveryAsync(EmailDelivery delivery, CancellationToken cancellationToken)
        {
            // Phase 46 — digest rows carry their own pre-rendered content (built once, at
            // generation time, from a live re-query of the user's authorized data — see
            // DigestGenerationService's own doc comment on why re-rendering per attempt would be
            // both wrong, since the window would shift, and redundant). Only the recipient's
            // continued existence needs re-checking here; there's no single task/project to
            // re-validate against for a multi-item digest.
            if (delivery.RenderedHtmlBody is not null)
            {
                var digestRecipient = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == delivery.RecipientUserId, cancellationToken);
                if (digestRecipient is null || !digestRecipient.IsActive)
                {
                    Cancel(delivery);
                    await _db.SaveChangesAsync(cancellationToken);
                    return;
                }

                var digestResult = await _emailService.SendAsync(digestRecipient.Email, digestRecipient.Name, delivery.RenderedSubject ?? delivery.Title, delivery.RenderedHtmlBody);
                ApplyResult(delivery, digestResult);
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var validation = await ValidateAsync(delivery, cancellationToken);
            if (validation is null)
            {
                Cancel(delivery);
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var (appName, appBaseUrl) = await ResolveBrandingAsync();
            var (subject, html) = await _templateService.RenderEmailAsync(
                delivery.NotificationType, validation.Variables, delivery.Title, delivery.Message, appName, appBaseUrl);

            var result = await _emailService.SendAsync(validation.RecipientEmail, validation.RecipientName, subject, html);
            ApplyResult(delivery, result);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task AttemptGroupedDeliveryAsync(IReadOnlyList<EmailDelivery> deliveries, CancellationToken cancellationToken)
        {
            // Every row in the group shares (RecipientUserId, NotificationType) by construction
            // (see EmailDeliveryBackgroundService) — validate against the first; if the recipient
            // is gone entirely there's nothing to send to any of them.
            var first = deliveries[0];
            var recipient = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == first.RecipientUserId, cancellationToken);
            if (recipient is null || !recipient.IsActive)
            {
                foreach (var delivery in deliveries)
                {
                    Cancel(delivery);
                }
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var (appName, appBaseUrl) = await ResolveBrandingAsync();
            var subject = $"{deliveries.Count} updates";
            var heading = $"You have {deliveries.Count} new notifications";
            var body = $"{deliveries.Count} recent updates are waiting for you in Silver Task.";
            var (renderedSubject, html) = NotificationTemplates.RenderCard(appName, appBaseUrl, subject, heading, body, "View Notifications", "/notifications", footerText: null);

            var result = await _emailService.SendAsync(recipient.Email, recipient.Name, renderedSubject, html);
            foreach (var delivery in deliveries)
            {
                ApplyResult(delivery, result);
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<(IReadOnlyList<EmailDelivery> Items, int TotalCount)> GetDeliveryLogAsync(int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.EmailDeliveries.Include(d => d.RecipientUser).OrderByDescending(d => d.QueuedAt);
            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        private sealed record ValidationResult(EmailTemplateVariables Variables, string RecipientEmail, string RecipientName);

        /// <summary>Returns null when the delivery should be cancelled outright (recipient
        /// gone/inactive, task/project deleted, or access revoked) rather than sent or retried —
        /// every one of these is a permanent, not transient, condition.</summary>
        private async Task<ValidationResult?> ValidateAsync(EmailDelivery delivery, CancellationToken cancellationToken)
        {
            var recipient = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == delivery.RecipientUserId, cancellationToken);
            if (recipient is null || !recipient.IsActive)
            {
                return null;
            }

            TaskItem? task = null;
            if (delivery.TaskId is Guid taskId)
            {
                task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
                if (task is null)
                {
                    return null; // Spec §65 — task deleted before delivery.
                }
                // Spec §63/§64 — a due-soon/overdue reminder must reflect the task's *current*
                // state; if it was completed/cancelled or is no longer due after being queued,
                // the reminder is now stale.
                var isReminderType = delivery.NotificationType is NotificationTypes.TaskDueSoon or NotificationTypes.TaskOverdue;
                if (isReminderType && (task.Status is TaskItemStatus.Complete or TaskItemStatus.Cancelled || task.DueDate is null))
                {
                    return null;
                }
            }

            Project? project = null;
            if (delivery.ProjectId is Guid projectId)
            {
                project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
                if (project is null)
                {
                    return null; // Spec §66 — project deleted before delivery.
                }

                try
                {
                    await _projectAccess.EnsureCanParticipateAsync(projectId, project.OwnerId, recipient.Id, recipient.Role);
                }
                catch (Exception ex) when (ex is ForbiddenException or NotFoundException)
                {
                    return null; // Spec §24/§25 — access revoked since the notification was queued.
                }
            }

            // Spec §23 — falls back to a generic "A former user" when the actor no longer exists
            // (SetNull'd ActorUserId's own nav coming back null) rather than omitting the
            // attribution entirely.
            var actorName = delivery.ActorUser?.Name ?? (delivery.ActorUserId is null ? "Silver Task" : "A former user");

            var recipientPreference = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == recipient.Id, cancellationToken);
            var dateFormat = recipientPreference?.DateFormat ?? "MM/dd/yyyy";

            var variables = new EmailTemplateVariables(
                UserName: recipient.Name,
                ActorName: actorName,
                TaskName: task?.Title,
                ProjectName: project?.Name,
                DueDate: task?.DueDate?.ToDateTime(TimeOnly.MinValue).ToString(dateFormat),
                ActionUrl: delivery.ActionUrl);

            return new ValidationResult(variables, recipient.Email, recipient.Name);
        }

        private static void ApplyResult(EmailDelivery delivery, EmailSendResult result)
        {
            delivery.AttemptCount++;
            if (result.Success)
            {
                delivery.Status = EmailDeliveryStatus.Sent;
                delivery.SentAt = DateTime.UtcNow;
                delivery.LastError = null;
                return;
            }

            delivery.LastError = result.ErrorMessage;
            if (delivery.AttemptCount >= MaxAttempts)
            {
                delivery.Status = EmailDeliveryStatus.Failed;
                delivery.FailedAt = DateTime.UtcNow;
            }
            else
            {
                delivery.Status = EmailDeliveryStatus.Queued;
                delivery.NextAttemptAt = DateTime.UtcNow.Add(RetryBackoff[delivery.AttemptCount - 1]);
            }
        }

        private static void Cancel(EmailDelivery delivery)
        {
            delivery.Status = EmailDeliveryStatus.Cancelled;
            delivery.FailedAt = DateTime.UtcNow;
        }

        private async Task<(string AppName, string AppBaseUrl)> ResolveBrandingAsync()
        {
            var appName = await _systemSettings.GetStringAsync(SystemSettingKeys.ApplicationName);
            var appBaseUrl = await AppUrlResolver.ResolveAsync(_systemSettings, _configuration);
            return (appName, appBaseUrl);
        }
    }
}
