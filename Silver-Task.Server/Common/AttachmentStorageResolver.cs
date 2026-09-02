namespace Silver_Task.Server.Common
{
    /// <summary>Phase 58 — extracted from AttachmentService's own former private
    /// ResolveStorageRoot so DiagnosticsService can report on the exact same path attachments are
    /// actually stored under, without duplicating (and risking drifting from) the resolution
    /// logic. Behavior is unchanged: Attachments:StorageRoot if configured, else
    /// App_Data/attachments under the content root.</summary>
    public static class AttachmentStorageResolver
    {
        public static string ResolveStorageRoot(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configuredRoot = configuration["Attachments:StorageRoot"];
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                configuredRoot = "App_Data/attachments";
            }
            return Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(environment.ContentRootPath, configuredRoot);
        }
    }
}
