import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '@/api/tasksApi';
import type { CreateTaskRequest } from '@/types/task';

const tasksKey = (projectId: string) => ['projects', projectId, 'tasks'] as const;

export function useTasks(projectId: string | undefined) {
  return useQuery({
    queryKey: tasksKey(projectId ?? ''),
    queryFn: () => tasksApi.list(projectId!),
    enabled: Boolean(projectId),
  });
}

export function useCreateTask(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTaskRequest) => tasksApi.create(projectId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tasksKey(projectId) });
    },
  });
}

export function useDeleteTask(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (taskId: string) => tasksApi.remove(taskId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tasksKey(projectId) });
    },
  });
}

export function useDuplicateTask(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (taskId: string) => tasksApi.duplicate(taskId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: tasksKey(projectId) });
    },
  });
}
