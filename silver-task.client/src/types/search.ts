import type { TaskPriority, TaskStatus } from './task';

export type SearchEntityType = 'task' | 'project' | 'user' | 'file' | 'comment' | 'tag' | 'template';

export const SEARCH_ENTITY_TYPES: SearchEntityType[] = ['task', 'project', 'user', 'file', 'comment', 'tag', 'template'];

export const SEARCH_ENTITY_LABELS: Record<SearchEntityType, string> = {
  task: 'Tasks',
  project: 'Projects',
  user: 'Users',
  file: 'Files',
  comment: 'Comments',
  tag: 'Tags',
  template: 'Templates',
};

export type SearchSort = 'relevance' | 'newest' | 'oldest' | 'updated' | 'dueDate';

export const SEARCH_SORT_OPTIONS: SearchSort[] = ['relevance', 'newest', 'oldest', 'updated', 'dueDate'];

export const SEARCH_SORT_LABELS: Record<SearchSort, string> = {
  relevance: 'Relevance',
  newest: 'Newest',
  oldest: 'Oldest',
  updated: 'Recently Updated',
  dueDate: 'Due Date',
};

/** Mirrors Silver-Task.Server/Models/DTOs/Search/SearchDtos.cs's SearchResultDto — a single flat
 * shape for every entity type, with type-specific fields left null where not applicable. */
export interface SearchResult {
  type: 'Task' | 'Project' | 'User' | 'File' | 'Comment' | 'Tag' | 'Template';
  id: string;
  title: string;
  snippet: string | null;
  actionUrl: string;
  score: number;
  projectId: string | null;
  projectName: string | null;
  status: TaskStatus | string | null;
  priority: TaskPriority | null;
  assigneeName: string | null;
  dueDate: string | null;
  tagNames: string[] | null;
  fileSizeBytes: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface SearchCounts {
  tasks: number;
  projects: number;
  users: number;
  files: number;
  comments: number;
  tags: number;
  templates: number;
}

export interface SearchResponse {
  query: string;
  total: number;
  page: number;
  pageSize: number;
  counts: SearchCounts;
  results: SearchResult[];
}

export interface SearchFilters {
  type?: SearchEntityType | 'all';
  projectId?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  assigneeId?: string;
  tagId?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
  sort?: SearchSort;
}
