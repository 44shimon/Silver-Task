import { useQuery } from '@tanstack/react-query';
import { adminApi } from '@/api/adminApi';

export function useAdminSystemRoles() {
  return useQuery({
    queryKey: ['admin', 'roles'],
    queryFn: adminApi.roles,
  });
}

export function useAdminProjectRoles() {
  return useQuery({
    queryKey: ['admin', 'project-roles'],
    queryFn: adminApi.projectRoles,
  });
}
