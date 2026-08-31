using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Entities;

namespace Silver_Task.Server.Models.DTOs.Email
{
    public static class EmailMappingExtensions
    {
        public static EmailTemplateDto ToDto(this EmailTemplate template)
        {
            var defaults = DefaultEmailTemplates.ByType.TryGetValue(template.NotificationType, out var notificationDefault)
                ? notificationDefault
                : DefaultDigestTemplates.ByType[template.NotificationType];
            return new EmailTemplateDto
            {
                NotificationType = template.NotificationType,
                SubjectTemplate = template.SubjectTemplate,
                HeadingTemplate = template.HeadingTemplate,
                BodyTemplate = template.BodyTemplate,
                CtaText = template.CtaText,
                FooterTemplate = template.FooterTemplate,
                DefaultSubject = defaults.Subject,
                DefaultHeading = defaults.Heading,
                DefaultBody = defaults.Body,
                DefaultCtaText = defaults.CtaText,
                IsCustomized = template.Id != Guid.Empty,
                UpdatedAt = template.Id == Guid.Empty ? null : template.UpdatedAt,
                UpdatedByName = template.UpdatedByUser?.Name
            };
        }

        public static EmailDeliveryDto ToDto(this EmailDelivery delivery) => new()
        {
            Id = delivery.Id,
            NotificationType = delivery.NotificationType,
            RecipientUserId = delivery.RecipientUserId,
            RecipientName = delivery.RecipientUser?.Name,
            Status = delivery.Status.ToString(),
            AttemptCount = delivery.AttemptCount,
            LastError = delivery.LastError,
            QueuedAt = delivery.QueuedAt,
            SentAt = delivery.SentAt,
            FailedAt = delivery.FailedAt
        };
    }
}
