import { httpClient } from './httpClient';
import type {
  AddProjectMemberRequest,
  CreateProjectRequest,
  Project,
  ProjectMember,
  UpdateProjectRequest,
} from '@/types/project';

export const projectsApi = {
  list: () => httpClient.get<Project[]>('/projects'),
  get: (id: string) => httpClient.get<Project>(`/projects/${id}`),
  create: (request: CreateProjectRequest) => httpClient.post<Project>('/projects', request),
  update: (id: string, request: UpdateProjectRequest) => httpClient.put<Project>(`/projects/${id}`, request),
  archive: (id: string) => httpClient.delete<void>(`/projects/${id}`),
  listMembers: (id: string) => httpClient.get<ProjectMember[]>(`/projects/${id}/members`),
  addMember: (id: string, request: AddProjectMemberRequest) =>
    httpClient.post<ProjectMember>(`/projects/${id}/members`, request),
  removeMember: (id: string, userId: string) => httpClient.delete<void>(`/projects/${id}/members/${userId}`),
};
