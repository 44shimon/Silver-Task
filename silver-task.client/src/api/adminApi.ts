import { httpClient } from './httpClient';
import type { AdminStats, RoleInfo, StorageHealth } from '@/types/admin';

export const adminApi = {
  stats: () => httpClient.get<AdminStats>('/admin/stats'),
  /** Permanent delete — distinct from projectsApi.archive, which is the regular soft delete. */
  deleteProject: (id: string) => httpClient.delete<void>(`/admin/projects/${id}`),
  /** The fixed system-role permission matrix (Phase 32) — see Admin -> Roles & Permissions. */
  roles: () => httpClient.get<RoleInfo[]>('/admin/roles'),
  /** The fixed project-role permission matrix. */
  projectRoles: () => httpClient.get<RoleInfo[]>('/admin/project-roles'),
  storageHealth: () => httpClient.get<StorageHealth>('/admin/storage/health'),
};
