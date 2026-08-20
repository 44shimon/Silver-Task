import { httpClient } from './httpClient';
import type { CreateTaskRequest, Task, UpdateTaskRequest } from '@/types/task';
import type { TaskActivity } from '@/types/activity';

export const tasksApi = {
  list: (projectId: string) => httpClient.get<Task[]>(`/projects/${projectId}/tasks`),
  create: (projectId: string, request: CreateTaskRequest) =>
    httpClient.post<Task>(`/projects/${projectId}/tasks`, request),
  update: (taskId: string, request: UpdateTaskRequest) => httpClient.put<Task>(`/tasks/${taskId}`, request),
  remove: (taskId: string) => httpClient.delete<void>(`/tasks/${taskId}`),
  duplicate: (taskId: string) => httpClient.post<Task>(`/tasks/${taskId}/duplicate`),
  setCustomValue: (taskId: string, customFieldId: string, value: string | null) =>
    httpClient.put<Task>(`/tasks/${taskId}/custom-values/${customFieldId}`, { value }),
  activities: (taskId: string) => httpClient.get<TaskActivity[]>(`/tasks/${taskId}/activities`),
};
