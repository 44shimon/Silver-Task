import { API_BASE, httpClient } from './httpClient';
import type { Attachment, AttachmentFilter, AttachmentList } from '@/types/attachment';

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
  return params.toString();
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
  uploadForProject: (projectId: string, file: File, onProgress?: (fraction: number) => void) => {
    const formData = new FormData();
    formData.append('file', file);
    return httpClient.uploadWithProgress<Attachment>(`/projects/${projectId}/files`, formData, onProgress);
  },

  listForComment: (commentId: string) => httpClient.get<Attachment[]>(`/comments/${commentId}/attachments`),
  uploadForComment: (commentId: string, file: File, onProgress?: (fraction: number) => void) => {
    const formData = new FormData();
    formData.append('file', file);
    return httpClient.uploadWithProgress<Attachment>(`/comments/${commentId}/attachments`, formData, onProgress);
  },
};
