import type { UserSummary } from './project';

export interface Comment {
  id: string;
  taskId: string;
  user: UserSummary;
  text: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCommentRequest {
  text: string;
}

export interface UpdateCommentRequest {
  text: string;
}
