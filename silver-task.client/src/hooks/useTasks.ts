import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '@/api/tasksApi';
import type { CreateTaskRequest, Task, UpdateTaskRequest } from '@/types/task';

const tasksKey = (projectId: string) => ['projects', projectId, 'tasks'] as const;

/** Fields Phase 7's inline editors can change. Status/Priority/AssignedTo get dropdown editors in Phase 8. */
export type EditableTaskFields = Partial<Pick<Task, 'title' | 'startDate' | 'dueDate'>>;

/** The backend PUT is a full-resource replace, so a single-field edit still has to carry
 * every other current value along with it. */
function buildUpdateRequest(task: Task, changes: EditableTaskFields): UpdateTaskRequest {
  return {
    title: task.title,
    description: task.description ?? undefined,
    status: task.status,
    priority: task.priority,
    assignedToUserId: task.assignedTo?.id ?? null,
    startDate: task.startDate,
    dueDate: task.dueDate,
    sortOrder: task.sortOrder,
    ...changes,
  };
}

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

interface UpdateTaskFieldInput {
  task: Task;
  changes: EditableTaskFields;
}

/**
 * Inline-cell edit: update the UI immediately, send the change to the API, and if it
 * fails, roll the cache back to its pre-edit snapshot so the cell reverts and the
 * caller can show an error (via this mutation's own isError state).
 */
export function useUpdateTask(projectId: string) {
  const queryClient = useQueryClient();
  const queryKey = tasksKey(projectId);

  return useMutation({
    mutationFn: ({ task, changes }: UpdateTaskFieldInput) => tasksApi.update(task.id, buildUpdateRequest(task, changes)),
    onMutate: async ({ task, changes }) => {
      await queryClient.cancelQueries({ queryKey });
      const previousTasks = queryClient.getQueryData<Task[]>(queryKey);

      queryClient.setQueryData<Task[]>(queryKey, (old) =>
        old?.map((t) => (t.id === task.id ? { ...t, ...changes } : t)),
      );

      return { previousTasks };
    },
    onError: (_error, _variables, context) => {
      if (context?.previousTasks) {
        queryClient.setQueryData(queryKey, context.previousTasks);
      }
    },
    onSuccess: (updatedTask) => {
      queryClient.setQueryData<Task[]>(queryKey, (old) =>
        old?.map((t) => (t.id === updatedTask.id ? updatedTask : t)),
      );
    },
  });
}
