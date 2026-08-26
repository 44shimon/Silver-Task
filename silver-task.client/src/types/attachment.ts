import type { UserSummary } from './project';
import type { FileCategory } from './fileCategory';
import type { Tag } from './tag';

export interface Attachment {
  id: string;
  projectId: string | null;
  /** Always resolves to the file's owning project, even for a task/comment attachment — see
   * AttachmentMappingExtensions.ResolveEffectiveProjectId (backend). Used for folder-move/
   * category pickers on cross-project views (Favorites, Recent). */
  effectiveProjectId: string;
  taskId: string | null;
  commentId: string | null;
  folderId: string | null;
  folderName: string | null;
  fileName: string;
  fileSize: number;
  mimeType: string;
  fileHash: string | null;
  description: string | null;
  category: FileCategory | null;
  tags: Tag[];
  /** Per-caller — never a property of the file itself server-side either (see
   * UserFileFavorite). */
  isFavorite: boolean;
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
  /** Undefined/omitted means root level — see attachmentsApi.listForProject. */
  folderId?: string;
  /** True searches folderId (or the whole project, if folderId is also omitted) and every
   * descendant folder — the "this folder and subfolders" / whole-project search scope. */
  includeSubfolders?: boolean;
  categoryId?: string;
  tagId?: string;
  favoritesOnly?: boolean;
}
