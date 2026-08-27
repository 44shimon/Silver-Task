import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminAutomationsApi, automationsApi } from '@/api/automationsApi';
import type { SaveAutomationRequest } from '@/types/automation';

// Fetches the full list per project/admin scope and lets the caller filter client-side (search/
// trigger/status/creator) — same "no pagination yet, nothing to gain from round-tripping per
// keystroke" convention useTaskFilters already established for the task grid.
const projectAutomationsKey = (projectId: string) => ['automations', 'project', projectId] as const;
const adminAutomationsKey = () => ['automations', 'admin'] as const;
const automationKey = (id: string) => ['automations', id] as const;
const runsKey = (id: string, page: number) => ['automations', id, 'runs', page] as const;

export function useProjectAutomations(projectId: string | undefined) {
  return useQuery({
    queryKey: projectAutomationsKey(projectId ?? ''),
    queryFn: () => automationsApi.listForProject(projectId!),
    enabled: Boolean(projectId),
  });
}

export function useAdminAutomations() {
  return useQuery({
    queryKey: adminAutomationsKey(),
    queryFn: () => adminAutomationsApi.listAll(),
  });
}

export function useAutomation(id: string | null) {
  return useQuery({
    queryKey: automationKey(id ?? ''),
    queryFn: () => automationsApi.getById(id!),
    enabled: !!id,
  });
}

function useInvalidateAutomations(projectId?: string) {
  const queryClient = useQueryClient();
  return () => {
    if (projectId) {
      queryClient.invalidateQueries({ queryKey: projectAutomationsKey(projectId) });
    }
    queryClient.invalidateQueries({ queryKey: adminAutomationsKey() });
  };
}

export function useCreateProjectAutomation(projectId: string) {
  const invalidate = useInvalidateAutomations(projectId);
  return useMutation({
    mutationFn: (request: SaveAutomationRequest) => automationsApi.createForProject(projectId, request),
    onSuccess: invalidate,
  });
}

export function useCreateGlobalAutomation() {
  const invalidate = useInvalidateAutomations();
  return useMutation({
    mutationFn: (request: SaveAutomationRequest) => adminAutomationsApi.create(request),
    onSuccess: invalidate,
  });
}

export function useUpdateAutomation(projectId?: string) {
  const invalidate = useInvalidateAutomations(projectId);
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: SaveAutomationRequest }) => automationsApi.update(id, request),
    onSuccess: (updated) => {
      queryClient.setQueryData(automationKey(updated.id), updated);
      invalidate();
    },
  });
}

export function useDeleteAutomation(projectId?: string) {
  const invalidate = useInvalidateAutomations(projectId);
  return useMutation({
    mutationFn: (id: string) => automationsApi.remove(id),
    onSuccess: invalidate,
  });
}

export function useSetAutomationActive(projectId?: string) {
  const invalidate = useInvalidateAutomations(projectId);
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      isActive ? automationsApi.enable(id) : automationsApi.disable(id),
    onSuccess: (updated) => {
      queryClient.setQueryData(automationKey(updated.id), updated);
      invalidate();
    },
  });
}

export function useDuplicateAutomation(projectId?: string) {
  const invalidate = useInvalidateAutomations(projectId);
  return useMutation({
    mutationFn: (id: string) => automationsApi.duplicate(id),
    onSuccess: invalidate,
  });
}

export function useAutomationRuns(id: string | null, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: runsKey(id ?? '', page),
    queryFn: () => automationsApi.runs(id!, page, pageSize),
    enabled: !!id,
  });
}

export function useRetryAutomationRun(automationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (runId: string) => automationsApi.retryRun(automationId, runId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['automations', automationId, 'runs'] });
      queryClient.invalidateQueries({ queryKey: automationKey(automationId) });
    },
  });
}

export function useTestAutomation(id: string) {
  return useMutation({
    mutationFn: (sampleEntityId: string) => automationsApi.test(id, sampleEntityId),
  });
}
