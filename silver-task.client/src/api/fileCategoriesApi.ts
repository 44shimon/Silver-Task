import { httpClient } from './httpClient';
import type { FileCategory } from '@/types/fileCategory';

export const fileCategoriesApi = {
  /** Active categories only, for category pickers — global, not project-scoped. */
  listActive: () => httpClient.get<FileCategory[]>('/file-categories'),
};

/** Admin -> File Categories (Administrator only). */
export const adminFileCategoriesApi = {
  listAll: () => httpClient.get<FileCategory[]>('/admin/file-categories'),
  create: (name: string, description?: string) =>
    httpClient.post<FileCategory>('/admin/file-categories', { name, description }),
  update: (id: string, name: string, description?: string) =>
    httpClient.put<FileCategory>(`/admin/file-categories/${id}`, { name, description }),
  deactivate: (id: string) => httpClient.post<FileCategory>(`/admin/file-categories/${id}/deactivate`),
  activate: (id: string) => httpClient.post<FileCategory>(`/admin/file-categories/${id}/activate`),
  remove: (id: string) => httpClient.delete<void>(`/admin/file-categories/${id}`),
};
