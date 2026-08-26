import type { UserSummary } from './project';

export interface Folder {
  id: string;
  name: string;
  parentFolderId: string | null;
  projectId: string;
  createdBy: UserSummary;
  isDeleted: boolean;
  deletedAt: string | null;
  deletedBy: UserSummary | null;
  createdAt: string;
  updatedAt: string;
}

export type FolderDeleteMode = 'MoveContentsToParent' | 'DeleteContents';

export interface FolderDeletePreview {
  fileCount: number;
  subfolderCount: number;
}

export interface CreateFolderRequest {
  name: string;
  parentFolderId?: string | null;
}
