import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tasksApi } from '@/api/tasksApi';
import type { CreateTaskRequest, Task, TaskPriority, TaskStatus, UpdateTaskRequest } from '@/types/task';
import type { UserSummary } from '@/types/project';

const tasksKey = (projectId: string) => ['projects', projectId, 'tasks'] as const;

/** The backend PUT is a full-resource replace, so a single-field edit still has to carry
 * every other current value along with it. */
function buildBaseRequest(task: Task): UpdateTaskRequest {
  return {
    title: task.title,
    description: task.description ?? undefined,
    status: task.status,
    priority: task.priority,
    assignedToUserId: task.assignedTo?.id ?? null,
    startDate: task.startDate,
    dueDate: task.dueDate,
    sortOrder: task.sortOrder,
  };
}

interface TaskFieldChange {
  /** Patched directly into the cached Task for the optimistic UI update. */
  optimistic: Partial<Task>;
  /** Merged into the full-replace PUT body. Differs from `optimistic` for assignee,
   * where the cache stores a UserSummary but the API wants a bare id. */
  request: Partial<UpdateTaskRequest>;
}

/** One constructor per inline-editable field, so every editor builds both the optimistic
 * cache patch and the API request the same way. */
export const taskFieldChange = {
  title: (value: string): TaskFieldChange => ({ optimistic: { title: value }, request: { title: value } }),
  description: (value: string | null): TaskFieldChange => ({
    optimistic: { description: value },
    request: { description: value ?? undefined },
  }),
  startDate: (value: string | null): TaskFieldChange => ({
    optimistic: { startDate: value },
    request: { startDate: value },
  }),
  dueDate: (value: string | null): TaskFieldChange => ({
    optimistic: { dueDate: value },
    request: { dueDate: value },
  }),
  status: (value: TaskStatus): TaskFieldChange => ({ optimistic: { status: value }, request: { status: value } }),
  priority: (value: TaskPriority): TaskFieldChange => ({
    optimistic: { priority: value },
    request: { priority: value },
  }),
  assignee: (member: UserSummary | null): TaskFieldChange => ({
    optimistic: { assignedTo: member },
    request: { assignedToUserId: member?.id ?? null },
  }),
};

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
  change: TaskFieldChange;
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
    mutationFn: ({ task, change }: UpdateTaskFieldInput) =>
      tasksApi.update(task.id, { ...buildBaseRequest(task), ...change.request }),
    onMutate: async ({ task, change }) => {
      await queryClient.cancelQueries({ queryKey });
      const previousTasks = queryClient.getQueryData<Task[]>(queryKey);

      queryClient.setQueryData<Task[]>(queryKey, (old) =>
        old?.map((t) => (t.id === task.id ? { ...t, ...change.optimistic } : t)),
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

interface SetCustomValueInput {
  task: Task;
  customFieldId: string;
  value: string | null;
}

/** Same optimistic-update/rollback shape as useUpdateTask, but for the separate
 * custom-value endpoint rather than the full-task PUT. */
export function useSetTaskCustomValue(projectId: string) {
  const queryClient = useQueryClient();
  const queryKey = tasksKey(projectId);

  return useMutation({
    mutationFn: ({ task, customFieldId, value }: SetCustomValueInput) =>
      tasksApi.setCustomValue(task.id, customFieldId, value),
    onMutate: async ({ task, customFieldId, value }) => {
      await queryClient.cancelQueries({ queryKey });
      const previousTasks = queryClient.getQueryData<Task[]>(queryKey);

      queryClient.setQueryData<Task[]>(queryKey, (old) =>
        old?.map((t) => {
          if (t.id !== task.id) {
            return t;
          }
          const otherValues = t.customValues.filter((v) => v.customFieldId !== customFieldId);
          return {
            ...t,
            customValues: value === null ? otherValues : [...otherValues, { customFieldId, value }],
          };
        }),
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
