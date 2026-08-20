import type { UserSummary } from './project';

export interface Attachment {
  id: string;
  taskId: string;
  fileName: string;
  fileSize: number;
  mimeType: string;
  uploadedBy: UserSummary;
  createdAt: string;
}
