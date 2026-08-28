import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '@/api/tasksApi';

const checklistKey = (taskId: string) => ['tasks', taskId, 'checklist'] as const;

export function useTaskChecklist(taskId: string) {
  return useQuery({
    queryKey: checklistKey(taskId),
    queryFn: () => tasksApi.checklist(taskId),
  });
}

export function useAddChecklistItem(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (text: string) => tasksApi.addChecklistItem(taskId, text),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: checklistKey(taskId) });
    },
  });
}

export function useSetChecklistItemChecked(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ itemId, isChecked }: { itemId: string; isChecked: boolean }) =>
      tasksApi.setChecklistItemChecked(taskId, itemId, isChecked),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: checklistKey(taskId) });
    },
  });
}

export function useRemoveChecklistItem(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (itemId: string) => tasksApi.removeChecklistItem(taskId, itemId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: checklistKey(taskId) });
    },
  });
}
