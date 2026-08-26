import { API_BASE, httpClient } from './httpClient';
import type { Attachment, AttachmentFilter, AttachmentList } from '@/types/attachment';
import type { Tag } from '@/types/tag';

function buildQuery(filter: AttachmentFilter): string {
  const params = new URLSearchParams();
  if (filter.search) params.set('search', filter.search);
  if (filter.type && filter.type !== 'all') params.set('type', filter.type);
  if (filter.uploadedByUserId) params.set('uploadedByUserId', filter.uploadedByUserId);
  if (filter.dateFrom) params.set('dateFrom', filter.dateFrom);
  if (filter.dateTo) params.set('dateTo', filter.dateTo);
  if (filter.onlyDeleted) params.set('onlyDeleted', 'true');
  if (filter.sortField) params.set('sortField', filter.sortField);
  params.set('sortDescending', String(filter.sortDescending ?? true));
  params.set('page', String(filter.page ?? 1));
  params.set('pageSize', String(filter.pageSize ?? 50));
  if (filter.folderId) params.set('folderId', filter.folderId);
  if (filter.includeSubfolders) params.set('includeSubfolders', 'true');
  if (filter.categoryId) params.set('categoryId', filter.categoryId);
  if (filter.tagId) params.set('tagId', filter.tagId);
  if (filter.favoritesOnly) params.set('favoritesOnly', 'true');
  return params.toString();
}

export interface BulkActionResult {
  succeededIds: string[];
  failed: { fileId: string; error: string }[];
}

export const attachmentsApi = {
  getById: (id: string) => httpClient.get<Attachment>(`/attachments/${id}`),
  rename: (id: string, fileName: string) => httpClient.put<Attachment>(`/attachments/${id}`, { fileName }),
  remove: (id: string) => httpClient.delete<void>(`/attachments/${id}`),
  restore: (id: string) => httpClient.post<Attachment>(`/attachments/${id}/restore`),
  /** Not fetched via httpClient — this is a direct, cookie-authenticated browser navigation URL. */
  downloadUrl: (id: string) => `${API_BASE}/attachments/${id}/download`,

  listForTask: (taskId: string) => httpClient.get<Attachment[]>(`/tasks/${taskId}/attachments`),
  uploadForTask: (taskId: string, file: File, onProgress?: (fraction: number) => void) => {
    const formData = new FormData();
    formData.append('file', file);
    return httpClient.uploadWithProgress<Attachment>(`/tasks/${taskId}/attachments`, formData, onProgress);
  },

  listForProject: (projectId: string, filter: AttachmentFilter = {}) =>
    httpClient.get<AttachmentList>(`/projects/${projectId}/files?${buildQuery(filter)}`),
  uploadForProject: (projectId: string, file: File, folderId?: string | null, onProgress?: (fraction: number) => void) => {
    const formData = new FormData();
    formData.append('file', file);
    if (folderId) formData.append('folderId', folderId);
    return httpClient.uploadWithProgress<Attachment>(`/projects/${projectId}/files`, formData, onProgress);
  },

  listForComment: (commentId: string) => httpClient.get<Attachment[]>(`/comments/${commentId}/attachments`),
  uploadForComment: (commentId: string, file: File, onProgress?: (fraction: number) => void) => {
    const formData = new FormData();
    formData.append('file', file);
    return httpClient.uploadWithProgress<Attachment>(`/comments/${commentId}/attachments`, formData, onProgress);
  },

  move: (id: string, folderId: string | null) => httpClient.post<Attachment>(`/attachments/${id}/move`, { folderId }),
  updateDescription: (id: string, description: string | null) =>
    httpClient.put<Attachment>(`/attachments/${id}/description`, { description }),
  setCategory: (id: string, categoryId: string | null) =>
    httpClient.put<Attachment>(`/attachments/${id}/category`, { categoryId }),

  getTags: (id: string) => httpClient.get<Tag[]>(`/attachments/${id}/tags`),
  addTag: (id: string, name: string) => httpClient.post<Tag>(`/attachments/${id}/tags`, { name }),
  removeTag: (id: string, tagId: string) => httpClient.delete<void>(`/attachments/${id}/tags/${tagId}`),

  favorite: (id: string) => httpClient.post<void>(`/attachments/${id}/favorite`),
  unfavorite: (id: string) => httpClient.delete<void>(`/attachments/${id}/favorite`),
  listFavorites: () => httpClient.get<Attachment[]>('/attachments/favorites'),
  listRecent: (limit = 50) => httpClient.get<Attachment[]>(`/attachments/recent?limit=${limit}`),

  bulkMove: (fileIds: string[], folderId: string | null) =>
    httpClient.post<BulkActionResult>('/attachments/bulk/move', { fileIds, folderId }),
  bulkTag: (fileIds: string[], tagName: string) =>
    httpClient.post<BulkActionResult>('/attachments/bulk/tag', { fileIds, tagName }),
  bulkUntag: (fileIds: string[], tagId: string) =>
    httpClient.post<BulkActionResult>('/attachments/bulk/untag', { fileIds, tagId }),
  bulkDelete: (fileIds: string[]) => httpClient.post<BulkActionResult>('/attachments/bulk/delete', { fileIds }),
  bulkFavorite: (fileIds: string[], favorite: boolean) =>
    httpClient.post<BulkActionResult>('/attachments/bulk/favorite', { fileIds, favorite }),
};
