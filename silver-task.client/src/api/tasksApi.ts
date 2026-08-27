import { httpClient } from './httpClient';
import type { CreateTaskRequest, Task, UpdateTaskRequest } from '@/types/task';
import type { TaskActivity } from '@/types/activity';
import type { Tag } from '@/types/tag';

export const tasksApi = {
  list: (projectId: string) => httpClient.get<Task[]>(`/projects/${projectId}/tasks`),
  /** Every task assigned to the current user across all their projects — backs My Tasks. */
  myTasks: () => httpClient.get<Task[]>('/tasks/my'),
  /** Global search (Topbar) — server-scoped and capped, never the whole task table. */
  search: (query: string) => httpClient.get<Task[]>(`/tasks/search?q=${encodeURIComponent(query)}`),
  create: (projectId: string, request: CreateTaskRequest) =>
    httpClient.post<Task>(`/projects/${projectId}/tasks`, request),
  update: (taskId: string, request: UpdateTaskRequest) => httpClient.put<Task>(`/tasks/${taskId}`, request),
  // deleteSubtasks=false (default) reparents direct children to this task's own parent instead
  // of removing them — the caller only ever sets it true after an explicit confirmation.
  remove: (taskId: string, deleteSubtasks = false) =>
    httpClient.delete<void>(`/tasks/${taskId}${deleteSubtasks ? '?deleteSubtasks=true' : ''}`),
  duplicate: (taskId: string) => httpClient.post<Task>(`/tasks/${taskId}/duplicate`),
  setCustomValue: (taskId: string, customFieldId: string, value: string | null) =>
    httpClient.put<Task>(`/tasks/${taskId}/custom-values/${customFieldId}`, { value }),
  activities: (taskId: string) => httpClient.get<TaskActivity[]>(`/tasks/${taskId}/activities`),
  subtasks: (taskId: string) => httpClient.get<Task[]>(`/tasks/${taskId}/subtasks`),
  createSubtask: (parentTaskId: string, request: CreateTaskRequest) =>
    httpClient.post<Task>(`/tasks/${parentTaskId}/subtasks`, request),
  /** null moves the task to top level. */
  setParent: (taskId: string, parentTaskId: string | null) =>
    httpClient.put<Task>(`/tasks/${taskId}/parent`, { parentTaskId }),
  setSortOrder: (taskId: string, sortOrder: number) =>
    httpClient.put<Task>(`/tasks/${taskId}/sort-order`, { sortOrder }),
  /** "Labels" (Phase 35) — reuses the same global Tag vocabulary Phase 34 introduced for files. */
  labels: (taskId: string) => httpClient.get<Tag[]>(`/tasks/${taskId}/labels`),
  addLabel: (taskId: string, name: string) => httpClient.post<Tag>(`/tasks/${taskId}/labels`, { name }),
  removeLabel: (taskId: string, tagId: string) => httpClient.delete<void>(`/tasks/${taskId}/labels/${tagId}`),
};
