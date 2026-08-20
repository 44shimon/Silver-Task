import { API_BASE, httpClient } from './httpClient';
import type { Attachment } from '@/types/attachment';

export const attachmentsApi = {
  list: (taskId: string) => httpClient.get<Attachment[]>(`/tasks/${taskId}/attachments`),
  upload: (taskId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return httpClient.upload<Attachment>(`/tasks/${taskId}/attachments`, formData);
  },
  remove: (id: string) => httpClient.delete<void>(`/attachments/${id}`),
  /** Not fetched via httpClient — this is a direct, cookie-authenticated browser navigation URL. */
  downloadUrl: (id: string) => `${API_BASE}/attachments/${id}/download`,
};
