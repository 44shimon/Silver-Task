import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminSettingsApi, publicSettingsApi } from '@/api/systemSettingsApi';

const systemSettingsKey = ['admin', 'settings'] as const;
const publicSettingsKey = ['settings', 'public'] as const;

export function useSystemSettings() {
  return useQuery({
    queryKey: systemSettingsKey,
    queryFn: adminSettingsApi.getAll,
  });
}

export function useUpdateSystemSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (values: Record<string, string>) => adminSettingsApi.update(values),
    onSuccess: (settings) => {
      queryClient.setQueryData(systemSettingsKey, settings);
      // ApplicationName/ApplicationDescription may have just changed — refetch the public
      // copy the Topbar/LoginPage read, rather than leaving it stale until its own staleTime.
      queryClient.invalidateQueries({ queryKey: publicSettingsKey });
    },
  });
}

/** Safe to call from anywhere, logged in or not — the backing endpoint is [AllowAnonymous] and
 * deliberately only ever returns the two branding fields (see PublicSettingsDto). */
export function usePublicSettings() {
  return useQuery({
    queryKey: publicSettingsKey,
    queryFn: publicSettingsApi.getPublic,
    staleTime: 5 * 60 * 1000,
  });
}
