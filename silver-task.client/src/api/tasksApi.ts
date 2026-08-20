import { httpClient } from './httpClient';
import type { CreateTaskRequest, Task, UpdateTaskRequest } from '@/types/task';

export const tasksApi = {
  list: (projectId: string) => httpClient.get<Task[]>(`/projects/${projectId}/tasks`),
  create: (projectId: string, request: CreateTaskRequest) =>
    httpClient.post<Task>(`/projects/${projectId}/tasks`, request),
  update: (taskId: string, request: UpdateTaskRequest) => httpClient.put<Task>(`/tasks/${taskId}`, request),
  remove: (taskId: string) => httpClient.delete<void>(`/tasks/${taskId}`),
  duplicate: (taskId: string) => httpClient.post<Task>(`/tasks/${taskId}/duplicate`),
};
