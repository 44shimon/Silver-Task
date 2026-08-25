namespace Silver_Task.Server.Models.DTOs.Settings
{
    /// <summary>Deliberately tiny — the only two system settings actually needed before/without
    /// authentication (the login page's branding). Never add anything sensitive here; this
    /// endpoint is [AllowAnonymous].</summary>
    public class PublicSettingsDto
    {
        public required string ApplicationName { get; set; }

        public required string ApplicationDescription { get; set; }
    }
}
