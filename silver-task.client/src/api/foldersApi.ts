import { httpClient } from './httpClient';
import type { CreateFolderRequest, Folder, FolderDeleteMode, FolderDeletePreview } from '@/types/folder';

export const foldersApi = {
  listForProject: (projectId: string, includeDeleted = false) =>
    httpClient.get<Folder[]>(`/projects/${projectId}/folders?includeDeleted=${includeDeleted}`),
  create: (projectId: string, request: CreateFolderRequest) =>
    httpClient.post<Folder>(`/projects/${projectId}/folders`, request),
  getById: (id: string) => httpClient.get<Folder>(`/folders/${id}`),
  rename: (id: string, name: string) => httpClient.put<Folder>(`/folders/${id}`, { name }),
  move: (id: string, parentFolderId: string | null) =>
    httpClient.post<Folder>(`/folders/${id}/move`, { parentFolderId }),
  getDeletePreview: (id: string) => httpClient.get<FolderDeletePreview>(`/folders/${id}/delete-preview`),
  remove: (id: string, mode: FolderDeleteMode) => httpClient.delete<void>(`/folders/${id}?mode=${mode}`),
  restore: (id: string) => httpClient.post<Folder>(`/folders/${id}/restore`),
};
