import { httpClient } from './httpClient';
import type {
  AddProjectMemberRequest,
  CreateProjectRequest,
  Project,
  ProjectMember,
  ProjectRole,
  UpdateProjectRequest,
} from '@/types/project';

export const projectsApi = {
  list: (includeArchived = false) =>
    httpClient.get<Project[]>(`/projects${includeArchived ? '?includeArchived=true' : ''}`),
  get: (id: string) => httpClient.get<Project>(`/projects/${id}`),
  create: (request: CreateProjectRequest) => httpClient.post<Project>('/projects', request),
  update: (id: string, request: UpdateProjectRequest) => httpClient.put<Project>(`/projects/${id}`, request),
  archive: (id: string) => httpClient.delete<void>(`/projects/${id}`),
  restore: (id: string) => httpClient.post<Project>(`/projects/${id}/restore`),
  listMembers: (id: string) => httpClient.get<ProjectMember[]>(`/projects/${id}/members`),
  addMember: (id: string, request: AddProjectMemberRequest) =>
    httpClient.post<ProjectMember>(`/projects/${id}/members`, request),
  removeMember: (id: string, userId: string) => httpClient.delete<void>(`/projects/${id}/members/${userId}`),
  setMemberRole: (id: string, userId: string, role: ProjectRole) =>
    httpClient.put<ProjectMember>(`/projects/${id}/members/${userId}/role`, { role }),
};
