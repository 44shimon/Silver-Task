import { httpClient } from './httpClient';
import type { TaskDependency, TaskDependencyEdge } from '@/types/dependency';

export const dependenciesApi = {
  listDependencies: (taskId: string) => httpClient.get<TaskDependency[]>(`/tasks/${taskId}/dependencies`),
  listDependents: (taskId: string) => httpClient.get<TaskDependency[]>(`/tasks/${taskId}/dependents`),
  create: (taskId: string, dependsOnTaskId: string) =>
    httpClient.post<TaskDependency>(`/tasks/${taskId}/dependencies`, { dependsOnTaskId }),
  remove: (taskId: string, dependencyId: string) =>
    httpClient.delete<void>(`/tasks/${taskId}/dependencies/${dependencyId}`),
  /** Every dependency edge in a project — backs Gantt/Timeline connector lines. */
  listProjectEdges: (projectId: string) => httpClient.get<TaskDependencyEdge[]>(`/projects/${projectId}/dependencies`),
};
