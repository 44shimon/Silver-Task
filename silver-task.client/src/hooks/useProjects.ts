import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { projectsApi } from '@/api/projectsApi';
import { adminApi } from '@/api/adminApi';
import type { AddProjectMemberRequest, CreateProjectRequest, ProjectRole, UpdateProjectRequest } from '@/types/project';

const projectsKey = ['projects'] as const;
const allProjectsKey = ['projects', 'all'] as const;
const projectKey = (id: string) => ['projects', id] as const;
const membersKey = (id: string) => ['projects', id, 'members'] as const;

export function useProjects() {
  return useQuery({
    queryKey: projectsKey,
    queryFn: () => projectsApi.list(),
  });
}

/** Every project the caller can see, including archived ones — for an Administrator that's
 * every project in the system. Backs the Admin Projects page. Kept as a separate query key
 * from `useProjects` so the sidebar's (unarchived-only) list isn't affected. */
export function useAllProjectsForAdmin() {
  return useQuery({
    queryKey: allProjectsKey,
    queryFn: () => projectsApi.list(true),
  });
}

export function useProject(id: string | undefined) {
  return useQuery({
    queryKey: projectKey(id ?? ''),
    queryFn: () => projectsApi.get(id!),
    enabled: Boolean(id),
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateProjectRequest) => projectsApi.create(request),
    onSuccess: () => {
      // TanStack Query's invalidation matching is prefix-based, so invalidating ['projects']
      // also covers ['projects', 'all'] (useAllProjectsForAdmin) below — no separate call needed.
      queryClient.invalidateQueries({ queryKey: projectsKey });
    },
  });
}

export function useUpdateProject(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateProjectRequest) => projectsApi.update(id, request),
    onSuccess: (project) => {
      queryClient.setQueryData(projectKey(id), project);
      queryClient.invalidateQueries({ queryKey: projectsKey });
    },
  });
}

export function useArchiveProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => projectsApi.archive(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectsKey });
    },
  });
}

export function useRestoreProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => projectsApi.restore(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectsKey });
    },
  });
}

/** Permanent delete (Administrator-only) — separate from useArchiveProject's soft delete. */
export function useDeleteProjectPermanently() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteProject(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectsKey });
    },
  });
}

export function useProjectMembers(id: string | undefined) {
  return useQuery({
    queryKey: membersKey(id ?? ''),
    queryFn: () => projectsApi.listMembers(id!),
    enabled: Boolean(id),
  });
}

export function useAddProjectMember(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddProjectMemberRequest) => projectsApi.addMember(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: membersKey(id) });
    },
  });
}

export function useRemoveProjectMember(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => projectsApi.removeMember(id, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: membersKey(id) });
    },
  });
}

export function useSetProjectMemberRole(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: ProjectRole }) =>
      projectsApi.setMemberRole(id, userId, role),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: membersKey(id) });
    },
  });
}
