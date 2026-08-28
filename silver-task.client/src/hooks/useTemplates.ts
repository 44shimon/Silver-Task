import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { projectTemplatesApi, taskTemplatesApi, templatesApi } from '@/api/templatesApi';
import { projectsApi } from '@/api/projectsApi';
import { useProjects } from './useProjects';
import type { UserSummary } from '@/types/project';
import type {
  CreateProjectFromTemplateRequest,
  CreateTaskFromTemplateRequest,
  SaveProjectTemplateRequest,
  SaveTaskTemplateRequest,
  ShareTemplateRequest,
} from '@/types/templates';

const templatesKey = ['templates'] as const;
const projectTemplateKey = (id: string) => ['project-templates', id] as const;
const taskTemplateKey = (id: string) => ['task-templates', id] as const;
const previewKey = (id: string, startDate: string) => ['project-templates', id, 'preview', startDate] as const;

export function useTemplatesList() {
  return useQuery({ queryKey: templatesKey, queryFn: templatesApi.list });
}

/** Every distinct active member across every project the caller belongs to — feeds the "Assign
 * to Specific User" picker in both template builders. Project Templates aren't scoped to any one
 * project, and this app has no general "list all users" endpoint available below Administrator
 * (GET /api/users is Administrator-only), so the candidate pool is drawn from the caller's own
 * projects' membership instead — the same already-authorized data every task assignee dropdown
 * already relies on, just aggregated across projects rather than scoped to one. */
export function useCollaborators(): UserSummary[] {
  const { data: projects } = useProjects();
  const projectIds = projects?.map((p) => p.id) ?? [];

  return useQueries({
    queries: projectIds.map((id) => ({
      queryKey: ['projects', id, 'members'] as const,
      queryFn: () => projectsApi.listMembers(id),
    })),
    combine: (results) => {
      const byId = new Map<string, UserSummary>();
      for (const result of results) {
        for (const member of result.data ?? []) {
          if (member.user.isActive) {
            byId.set(member.user.id, member.user);
          }
        }
      }
      return Array.from(byId.values()).sort((a, b) => a.name.localeCompare(b.name));
    },
  });
}

// ---------- Project Templates ----------

export function useProjectTemplate(id: string | undefined) {
  return useQuery({
    queryKey: projectTemplateKey(id ?? ''),
    queryFn: () => projectTemplatesApi.get(id!),
    enabled: Boolean(id),
  });
}

export function useCreateProjectTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveProjectTemplateRequest) => projectTemplatesApi.create(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useUpdateProjectTemplate(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveProjectTemplateRequest) => projectTemplatesApi.update(id, request),
    onSuccess: (template) => {
      queryClient.setQueryData(projectTemplateKey(id), template);
      queryClient.invalidateQueries({ queryKey: templatesKey });
    },
  });
}

export function useDeleteProjectTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => projectTemplatesApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useSetProjectTemplateArchived() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) =>
      archived ? projectTemplatesApi.archive(id) : projectTemplatesApi.restore(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useDuplicateProjectTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => projectTemplatesApi.duplicate(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useShareProjectTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ShareTemplateRequest }) => projectTemplatesApi.share(id, request),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: templatesKey });
      queryClient.invalidateQueries({ queryKey: projectTemplateKey(id) });
    },
  });
}

export function useUnshareProjectTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, userId }: { id: string; userId: string }) => projectTemplatesApi.unshare(id, userId),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: templatesKey });
      queryClient.invalidateQueries({ queryKey: projectTemplateKey(id) });
    },
  });
}

export function useToggleProjectTemplateFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) =>
      favorite ? projectTemplatesApi.favorite(id) : projectTemplatesApi.unfavorite(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useProjectTemplatePreview(id: string | undefined, startDate: string | undefined) {
  return useQuery({
    queryKey: previewKey(id ?? '', startDate ?? ''),
    queryFn: () => projectTemplatesApi.preview(id!, startDate!),
    enabled: Boolean(id && startDate),
  });
}

export function useInstantiateProjectFromTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateProjectFromTemplateRequest) => projectTemplatesApi.instantiate(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: templatesKey });
      queryClient.invalidateQueries({ queryKey: ['projects'] });
    },
  });
}

// ---------- Task Templates ----------

export function useTaskTemplate(id: string | undefined) {
  return useQuery({
    queryKey: taskTemplateKey(id ?? ''),
    queryFn: () => taskTemplatesApi.get(id!),
    enabled: Boolean(id),
  });
}

export function useCreateTaskTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveTaskTemplateRequest) => taskTemplatesApi.create(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useUpdateTaskTemplate(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveTaskTemplateRequest) => taskTemplatesApi.update(id, request),
    onSuccess: (template) => {
      queryClient.setQueryData(taskTemplateKey(id), template);
      queryClient.invalidateQueries({ queryKey: templatesKey });
    },
  });
}

export function useDeleteTaskTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => taskTemplatesApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useSetTaskTemplateArchived() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) =>
      archived ? taskTemplatesApi.archive(id) : taskTemplatesApi.restore(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useDuplicateTaskTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => taskTemplatesApi.duplicate(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useShareTaskTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ShareTemplateRequest }) => taskTemplatesApi.share(id, request),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: templatesKey });
      queryClient.invalidateQueries({ queryKey: taskTemplateKey(id) });
    },
  });
}

export function useUnshareTaskTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, userId }: { id: string; userId: string }) => taskTemplatesApi.unshare(id, userId),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: templatesKey });
      queryClient.invalidateQueries({ queryKey: taskTemplateKey(id) });
    },
  });
}

export function useToggleTaskTemplateFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) =>
      favorite ? taskTemplatesApi.favorite(id) : taskTemplatesApi.unfavorite(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: templatesKey }),
  });
}

export function useInstantiateTaskFromTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTaskFromTemplateRequest) => taskTemplatesApi.instantiate(request),
    onSuccess: (_data, { projectId }) => {
      queryClient.invalidateQueries({ queryKey: templatesKey });
      queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tasks'] });
      queryClient.invalidateQueries({ queryKey: ['tasks', 'my'] });
    },
  });
}
