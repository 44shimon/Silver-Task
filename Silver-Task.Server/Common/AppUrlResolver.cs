using Silver_Task.Server.Services;

namespace Silver_Task.Server.Common
{
    /// <summary>Phase 45 — resolves the base URL email links are built against. Prefers the
    /// admin-configured General.ApplicationBaseUrl (added this phase specifically because the
    /// previous Cors:AllowedOrigins-derived fallback is fragile — that setting is empty by
    /// default and exists to configure CORS, not branding/links); falls back to the first
    /// configured CORS origin for deployments that set that but never set the new setting, so
    /// existing email links don't silently break on upgrade.</summary>
    public static class AppUrlResolver
    {
        public static async Task<string> ResolveAsync(ISystemSettingsService systemSettings, IConfiguration configuration)
        {
            var configured = await systemSettings.GetStringAsync(SystemSettingKeys.ApplicationBaseUrl);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }
            return configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault() ?? "";
        }
    }
}
