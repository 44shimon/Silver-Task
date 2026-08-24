import { httpClient } from './httpClient';
import type { AdminStats } from '@/types/admin';

export const adminApi = {
  stats: () => httpClient.get<AdminStats>('/admin/stats'),
  /** Permanent delete — distinct from projectsApi.archive, which is the regular soft delete. */
  deleteProject: (id: string) => httpClient.delete<void>(`/admin/projects/${id}`),
};
