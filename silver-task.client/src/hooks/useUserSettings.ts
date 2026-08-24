import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { userSettingsApi } from '@/api/userSettingsApi';
import type { ChangePasswordRequest, NotificationSetting, UpdatePreferencesRequest, UpdateProfileRequest } from '@/types/settings';

const CURRENT_USER_QUERY_KEY = ['auth', 'me'];
const preferencesKey = ['users', 'me', 'preferences'] as const;
const notificationsKey = ['users', 'me', 'notifications'] as const;

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateProfileRequest) => userSettingsApi.updateProfile(request),
    onSuccess: (user) => {
      // Same query key useCurrentUser reads — the Topbar/Sidebar reflect the new name
      // immediately instead of waiting out useCurrentUser's 5-minute staleTime.
      queryClient.setQueryData(CURRENT_USER_QUERY_KEY, user);
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (request: ChangePasswordRequest) => userSettingsApi.changePassword(request),
  });
}

export function useUserPreferences() {
  return useQuery({
    queryKey: preferencesKey,
    queryFn: userSettingsApi.getPreferences,
  });
}

export function useUpdatePreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdatePreferencesRequest) => userSettingsApi.updatePreferences(request),
    onSuccess: (preferences) => {
      queryClient.setQueryData(preferencesKey, preferences);
    },
  });
}

export function useNotificationSettings() {
  return useQuery({
    queryKey: notificationsKey,
    queryFn: userSettingsApi.getNotificationSettings,
  });
}

export function useUpdateNotificationSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (settings: NotificationSetting[]) => userSettingsApi.updateNotificationSettings(settings),
    onSuccess: (settings) => {
      queryClient.setQueryData(notificationsKey, settings);
    },
  });
}
