import { httpClient } from './httpClient';
import type { SearchFilters, SearchResponse } from '@/types/search';

function buildQuery(q: string, filters: SearchFilters): string {
  const params = new URLSearchParams();
  params.set('q', q);
  if (filters.type) params.set('type', filters.type);
  if (filters.projectId) params.set('projectId', filters.projectId);
  if (filters.status) params.set('status', filters.status);
  if (filters.priority) params.set('priority', filters.priority);
  if (filters.assigneeId) params.set('assigneeId', filters.assigneeId);
  if (filters.tagId) params.set('tagId', filters.tagId);
  if (filters.dateFrom) params.set('dateFrom', filters.dateFrom);
  if (filters.dateTo) params.set('dateTo', filters.dateTo);
  if (filters.page) params.set('page', String(filters.page));
  if (filters.pageSize) params.set('pageSize', String(filters.pageSize));
  if (filters.sort) params.set('sort', filters.sort);
  return `?${params.toString()}`;
}

export const searchApi = {
  search: (q: string, filters: SearchFilters = {}) => httpClient.get<SearchResponse>(`/search${buildQuery(q, filters)}`),
};
