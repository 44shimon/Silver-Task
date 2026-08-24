import { httpClient } from './httpClient';
import type {
  ChangePasswordRequest,
  NotificationSetting,
  UpdatePreferencesRequest,
  UpdateProfileRequest,
  UserPreferences,
} from '@/types/settings';
import type { CurrentUser } from '@/types/auth';

/** All self-service — every call resolves to the caller's own account server-side
 * (User.GetUserId() from the auth cookie), never a user id supplied here. */
export const userSettingsApi = {
  updateProfile: (request: UpdateProfileRequest) => httpClient.put<CurrentUser>('/users/me', request),
  changePassword: (request: ChangePasswordRequest) => httpClient.post<void>('/users/me/change-password', request),
  getPreferences: () => httpClient.get<UserPreferences>('/users/me/preferences'),
  updatePreferences: (request: UpdatePreferencesRequest) =>
    httpClient.put<UserPreferences>('/users/me/preferences', request),
  getNotificationSettings: () => httpClient.get<NotificationSetting[]>('/users/me/notifications'),
  updateNotificationSettings: (settings: NotificationSetting[]) =>
    httpClient.put<NotificationSetting[]>('/users/me/notifications', { settings }),
};
