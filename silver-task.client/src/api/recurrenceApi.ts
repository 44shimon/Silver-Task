import { httpClient } from './httpClient';
import type { Task } from '@/types/task';
import type { CreateRecurrenceRequest, RecurrenceRule, UpdateRecurrenceRequest } from '@/types/recurrence';

export const recurrenceApi = {
  /** Null if the task isn't part of a recurring series. */
  get: (taskId: string) => httpClient.get<RecurrenceRule | null>(`/tasks/${taskId}/recurrence`),
  /** Attaches a recurrence rule to an existing task, which becomes the series' first occurrence. */
  create: (taskId: string, request: CreateRecurrenceRequest) =>
    httpClient.post<RecurrenceRule>(`/tasks/${taskId}/recurrence`, request),
  /** taskId can be any occurrence in the series, not just the first. */
  update: (taskId: string, request: UpdateRecurrenceRequest) =>
    httpClient.put<RecurrenceRule>(`/tasks/${taskId}/recurrence`, request),
  /** Hard-deletes the rule; every already-generated task is kept, just unlinked from the series. */
  remove: (taskId: string) => httpClient.delete<void>(`/tasks/${taskId}/recurrence`),
  stop: (taskId: string) => httpClient.post<RecurrenceRule>(`/tasks/${taskId}/recurrence/stop`),
  resume: (taskId: string) => httpClient.post<RecurrenceRule>(`/tasks/${taskId}/recurrence/resume`),
  series: (taskId: string) => httpClient.get<Task[]>(`/tasks/${taskId}/recurrence/series`),
  forProject: (projectId: string) => httpClient.get<RecurrenceRule[]>(`/projects/${projectId}/recurring-tasks`),
};
