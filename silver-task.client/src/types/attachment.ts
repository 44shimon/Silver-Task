import type { UserSummary } from './project';

export interface Attachment {
  id: string;
  projectId: string | null;
  taskId: string | null;
  commentId: string | null;
  fileName: string;
  fileSize: number;
  mimeType: string;
  fileHash: string | null;
  uploadedBy: UserSummary;
  isDeleted: boolean;
  deletedAt: string | null;
  deletedBy: UserSummary | null;
  /** Human-readable "Project → Task" (or "Project → Task (comment)") breadcrumb, computed
   * server-side — see AttachmentMappingExtensions.DescribeLocation. */
  location: string;
  createdAt: string;
  updatedAt: string;
}

export interface AttachmentList {
  items: Attachment[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** File-type buckets the Project Files filter offers — derived client-side from mimeType/
 * fileName extension, not a separate stored field. */
export type AttachmentTypeFilter = 'all' | 'pdf' | 'image' | 'spreadsheet' | 'document' | 'archive' | 'other';

export type AttachmentDateFilter = 'all' | 'today' | '7days' | '30days' | 'custom';

export type AttachmentSortField = 'date' | 'name' | 'size' | 'type' | 'uploadedBy';

export interface AttachmentFilter {
  search?: string;
  type?: AttachmentTypeFilter;
  uploadedByUserId?: string;
  dateFilter?: AttachmentDateFilter;
  dateFrom?: string;
  dateTo?: string;
  onlyDeleted?: boolean;
  page?: number;
  pageSize?: number;
  sortField?: AttachmentSortField;
  sortDescending?: boolean;
}
