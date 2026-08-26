import { httpClient } from './httpClient';
import type { Tag } from '@/types/tag';

export const tagsApi = {
  /** Active tags only, for "add tag" pickers — global, not project-scoped. */
  listActive: () => httpClient.get<Tag[]>('/tags'),
};

/** Admin -> Tags (Administrator only, matches AdminTagsController's [Authorize]). */
export const adminTagsApi = {
  listAll: () => httpClient.get<Tag[]>('/admin/tags'),
  rename: (id: string, name: string) => httpClient.put<Tag>(`/admin/tags/${id}`, { name }),
  deactivate: (id: string) => httpClient.post<Tag>(`/admin/tags/${id}/deactivate`),
  activate: (id: string) => httpClient.post<Tag>(`/admin/tags/${id}/activate`),
  remove: (id: string) => httpClient.delete<void>(`/admin/tags/${id}`),
};
