import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '@/api/tasksApi';

const labelsKey = (taskId: string) => ['tasks', taskId, 'labels'] as const;

export function useTaskLabels(taskId: string) {
  return useQuery({
    queryKey: labelsKey(taskId),
    queryFn: () => tasksApi.labels(taskId),
  });
}

export function useAddTaskLabel(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => tasksApi.addLabel(taskId, name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: labelsKey(taskId) });
    },
  });
}

export function useRemoveTaskLabel(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (tagId: string) => tasksApi.removeLabel(taskId, tagId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: labelsKey(taskId) });
    },
  });
}
