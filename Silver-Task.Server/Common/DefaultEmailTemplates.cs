namespace Silver_Task.Server.Common
{
    public record DefaultEmailTemplate(string Subject, string Heading, string Body, string CtaText);

    /// <summary>Phase 45 — the built-in copy for the notification types the admin Email
    /// Templates screen lets an Administrator override (spec's own §16/§17-22 example copy).
    /// EmailTemplateService falls back to these whenever a type has no EmailTemplate row, or a
    /// row exists but a given field on it is null ("use the default for just this field") — and
    /// EmailTemplateService.ResetAsync restores this exact copy by deleting the override row.
    /// Every other notification type (the other ~18 in NotificationTypes.All) keeps using the
    /// original generic NotificationTemplates.ForNotification(Title, Message, ...) rendering —
    /// this table is deliberately not exhaustive, only the types called out as customizable.</summary>
    public static class DefaultEmailTemplates
    {
        public static readonly IReadOnlyDictionary<string, DefaultEmailTemplate> ByType = new Dictionary<string, DefaultEmailTemplate>
        {
            [NotificationTypes.TaskAssigned] = new(
                Subject: "You were assigned: {{TaskName}}",
                Heading: "You have been assigned a task",
                Body: "{{ActorName}} assigned you:\n\n{{TaskName}}\n\nProject: {{ProjectName}}\n\nDue: {{DueDate}}",
                CtaText: "Open Task"),

            [NotificationTypes.MentionedInComment] = new(
                Subject: "You were mentioned in: {{TaskName}}",
                Heading: "You were mentioned",
                Body: "{{ActorName}} mentioned you in a comment.\n\nTask: {{TaskName}}",
                CtaText: "View Comment"),

            [NotificationTypes.TaskDueSoon] = new(
                Subject: "Task due soon: {{TaskName}}",
                Heading: "Task Due Soon",
                Body: "{{TaskName}}\n\nDue: {{DueDate}}",
                CtaText: "Open Task"),

            [NotificationTypes.TaskOverdue] = new(
                Subject: "Task overdue: {{TaskName}}",
                Heading: "Task Overdue",
                Body: "{{TaskName}} is overdue.\n\nDue: {{DueDate}}",
                CtaText: "Open Task"),

            [NotificationTypes.UserAddedToProject] = new(
                Subject: "You were added to: {{ProjectName}}",
                Heading: "You were added to a project",
                Body: "Project: {{ProjectName}}",
                CtaText: "Open Project")
        };
    }
}
