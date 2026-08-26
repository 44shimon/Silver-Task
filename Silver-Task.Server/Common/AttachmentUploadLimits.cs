namespace Silver_Task.Server.Common
{
    /// <summary>The hard, framework-level request-body ceiling ([RequestSizeLimit]) shared by
    /// every attachment upload endpoint (Task/Project/Comment) — comfortably above the highest
    /// value SystemSettingsService.ValidateIntBounds allows for Attachments.MaxSizeMb (500 MB),
    /// so the real, admin-configurable limit is always what actually rejects an oversized upload
    /// with a clean ValidationException, not a raw framework-level 413 first. Defined once here
    /// (not duplicated as a literal per controller) so the two can never drift out of sync.</summary>
    public static class AttachmentUploadLimits
    {
        public const long MaxRequestBodyBytes = 600 * 1024 * 1024;
    }
}
