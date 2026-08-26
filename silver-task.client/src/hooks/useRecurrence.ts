import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { recurrenceApi } from '@/api/recurrenceApi';
import { invalidateTaskHierarchyData } from './useTasks';
import type { CreateRecurrenceRequest, UpdateRecurrenceRequest } from '@/types/recurrence';

const recurrenceKey = (taskId: string) => ['tasks', taskId, 'recurrence'] as const;
const seriesKey = (taskId: string) => ['tasks', taskId, 'recurrence', 'series'] as const;
const projectRecurringTasksKey = (projectId: string) => ['projects', projectId, 'recurring-tasks'] as const;

export function useRecurrenceRule(taskId: string | undefined) {
  return useQuery({
    queryKey: recurrenceKey(taskId ?? ''),
    queryFn: () => recurrenceApi.get(taskId!),
    enabled: Boolean(taskId),
  });
}

export function useRecurrenceSeries(taskId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: seriesKey(taskId ?? ''),
    queryFn: () => recurrenceApi.series(taskId!),
    enabled: Boolean(taskId) && enabled,
  });
}

export function useProjectRecurringTasks(projectId: string) {
  return useQuery({
    queryKey: projectRecurringTasksKey(projectId),
    queryFn: () => recurrenceApi.forProject(projectId),
  });
}

/** Every mutation below invalidates broadly (the full task-hierarchy set plus the
 * recurrence-specific caches) rather than trying to track exactly which generated occurrences
 * changed — the same reasoning useTasks.ts's invalidateTaskHierarchyData already documents for
 * move/reorder/subtask-create: these are infrequent, structural actions where extra invalidation
 * breadth costs little. */
function invalidateRecurrenceData(queryClient: ReturnType<typeof useQueryClient>, projectId: string, taskId: string) {
  invalidateTaskHierarchyData(queryClient, projectId);
  queryClient.invalidateQueries({ queryKey: recurrenceKey(taskId) });
  queryClient.invalidateQueries({ queryKey: seriesKey(taskId) });
  queryClient.invalidateQueries({ queryKey: projectRecurringTasksKey(projectId) });
}

export function useCreateRecurrence(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ taskId, request }: { taskId: string; request: CreateRecurrenceRequest }) =>
      recurrenceApi.create(taskId, request),
    onSuccess: (_result, { taskId }) => invalidateRecurrenceData(queryClient, projectId, taskId),
  });
}

export function useUpdateRecurrence(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ taskId, request }: { taskId: string; request: UpdateRecurrenceRequest }) =>
      recurrenceApi.update(taskId, request),
    onSuccess: (_result, { taskId }) => invalidateRecurrenceData(queryClient, projectId, taskId),
  });
}

export function useStopRecurrence(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (taskId: string) => recurrenceApi.stop(taskId),
    onSuccess: (_result, taskId) => invalidateRecurrenceData(queryClient, projectId, taskId),
  });
}

export function useResumeRecurrence(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (taskId: string) => recurrenceApi.resume(taskId),
    onSuccess: (_result, taskId) => invalidateRecurrenceData(queryClient, projectId, taskId),
  });
}

export function useDeleteRecurrence(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (taskId: string) => recurrenceApi.remove(taskId),
    onSuccess: (_result, taskId) => invalidateRecurrenceData(queryClient, projectId, taskId),
  });
}
