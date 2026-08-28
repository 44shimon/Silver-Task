import { API_BASE, httpClient } from './httpClient';
import type { Project } from '@/types/project';
import type { Task } from '@/types/task';
import type {
  CreateProjectFromTemplateRequest,
  CreateTaskFromTemplateRequest,
  ProjectTemplate,
  ProjectTemplatePreview,
  SaveProjectTemplateRequest,
  SaveTaskTemplateRequest,
  ShareTemplateRequest,
  TaskTemplate,
  TemplateSummary,
} from '@/types/templates';

/** The Template Home's unified list (both ProjectTemplate and TaskTemplate). Per-type CRUD lives
 * on projectTemplatesApi/taskTemplatesApi below — mirrors TemplatesController's own doc comment. */
export const templatesApi = {
  list: () => httpClient.get<TemplateSummary[]>('/templates'),
};

export const projectTemplatesApi = {
  get: (id: string) => httpClient.get<ProjectTemplate>(`/project-templates/${id}`),
  create: (request: SaveProjectTemplateRequest) => httpClient.post<ProjectTemplate>('/project-templates', request),
  update: (id: string, request: SaveProjectTemplateRequest) => httpClient.put<ProjectTemplate>(`/project-templates/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/project-templates/${id}`),
  archive: (id: string) => httpClient.post<ProjectTemplate>(`/project-templates/${id}/archive`),
  restore: (id: string) => httpClient.post<ProjectTemplate>(`/project-templates/${id}/restore`),
  duplicate: (id: string) => httpClient.post<ProjectTemplate>(`/project-templates/${id}/duplicate`),
  share: (id: string, request: ShareTemplateRequest) => httpClient.post<void>(`/project-templates/${id}/share`, request),
  unshare: (id: string, userId: string) => httpClient.delete<void>(`/project-templates/${id}/share/${userId}`),
  favorite: (id: string) => httpClient.post<void>(`/project-templates/${id}/favorite`),
  unfavorite: (id: string) => httpClient.delete<void>(`/project-templates/${id}/favorite`),
  /** Not a fetch — a direct, same-origin download URL for an <a>, same pattern as
   * attachmentsApi.downloadUrl/reportsApi.exportUrl. */
  exportUrl: (id: string) => `${API_BASE}/project-templates/${id}/export`,
  preview: (id: string, startDate: string) =>
    httpClient.get<ProjectTemplatePreview>(`/project-templates/${id}/preview?startDate=${encodeURIComponent(startDate)}`),
  instantiate: (request: CreateProjectFromTemplateRequest) => httpClient.post<Project>('/project-templates/instantiate', request),
};

export const taskTemplatesApi = {
  get: (id: string) => httpClient.get<TaskTemplate>(`/task-templates/${id}`),
  create: (request: SaveTaskTemplateRequest) => httpClient.post<TaskTemplate>('/task-templates', request),
  update: (id: string, request: SaveTaskTemplateRequest) => httpClient.put<TaskTemplate>(`/task-templates/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/task-templates/${id}`),
  archive: (id: string) => httpClient.post<TaskTemplate>(`/task-templates/${id}/archive`),
  restore: (id: string) => httpClient.post<TaskTemplate>(`/task-templates/${id}/restore`),
  duplicate: (id: string) => httpClient.post<TaskTemplate>(`/task-templates/${id}/duplicate`),
  share: (id: string, request: ShareTemplateRequest) => httpClient.post<void>(`/task-templates/${id}/share`, request),
  unshare: (id: string, userId: string) => httpClient.delete<void>(`/task-templates/${id}/share/${userId}`),
  favorite: (id: string) => httpClient.post<void>(`/task-templates/${id}/favorite`),
  unfavorite: (id: string) => httpClient.delete<void>(`/task-templates/${id}/favorite`),
  instantiate: (request: CreateTaskFromTemplateRequest) => httpClient.post<Task>('/task-templates/instantiate', request),
};
