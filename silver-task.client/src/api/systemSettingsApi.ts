import { httpClient } from './httpClient';
import type { PublicSettings, SystemSetting } from '@/types/settings';

/** Administrator-only — matches AdminSettingsController's [Authorize(Roles=Administrator)]. */
export const adminSettingsApi = {
  getAll: () => httpClient.get<SystemSetting[]>('/admin/settings'),
  update: (values: Record<string, string>) =>
    httpClient.put<SystemSetting[]>('/admin/settings', { values }),
};

/** [AllowAnonymous] on the server — safe to call before login (branding on the login page). */
export const publicSettingsApi = {
  getPublic: () => httpClient.get<PublicSettings>('/settings/public'),
};
