export interface UserSummary {
  id: string;
  name: string;
  email: string;
}

export interface Project {
  id: string;
  name: string;
  description: string | null;
  owner: UserSummary;
  memberCount: number;
  /** Only populated by the endpoints that bother computing it (the Admin Projects list) — null elsewhere. */
  taskCount: number | null;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectMember {
  id: string;
  projectId: string;
  user: UserSummary;
  createdAt: string;
}

export interface CreateProjectRequest {
  name: string;
  description?: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string;
}

export interface AddProjectMemberRequest {
  email: string;
}

/** Fallback for when AddProjectMemberRequest 404s because no account exists yet — creates one
 * (always Member role) and adds them in one step. Administrator-only. */
export interface InviteMemberRequest {
  name: string;
  email: string;
  password: string;
}
