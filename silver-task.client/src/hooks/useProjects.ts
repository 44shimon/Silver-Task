import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { projectsApi } from '@/api/projectsApi';
import type { AddProjectMemberRequest, CreateProjectRequest, UpdateProjectRequest } from '@/types/project';

const projectsKey = ['projects'] as const;
const projectKey = (id: string) => ['projects', id] as const;
const membersKey = (id: string) => ['projects', id, 'members'] as const;

export function useProjects() {
  return useQuery({
    queryKey: projectsKey,
    queryFn: projectsApi.list,
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
