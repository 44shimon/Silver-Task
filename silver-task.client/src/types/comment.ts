import type { UserSummary } from './project';

export interface Comment {
  id: string;
  taskId: string;
  user: UserSummary;
  text: string;
  /** True for comments an Automation's "Add Comment" action posted — the UI marks these with an
   * "⚙ Automation" badge rather than blending them in as if a person typed them. */
  isAutomated: boolean;
  automationId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCommentRequest {
  text: string;
}

export interface UpdateCommentRequest {
  text: string;
}
