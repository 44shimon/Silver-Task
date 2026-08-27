import { httpClient } from './httpClient';
import type {
  Automation,
  AutomationExecutionList,
  AutomationQueryParams,
  AutomationTestResult,
  SaveAutomationRequest,
} from '@/types/automation';

function buildQuery(params?: AutomationQueryParams): string {
  if (!params) return '';
  const search = new URLSearchParams();
  if (params.search) search.set('search', params.search);
  if (params.triggerType) search.set('triggerType', params.triggerType);
  if (params.isActive !== undefined) search.set('isActive', String(params.isActive));
  if (params.createdByUserId) search.set('createdByUserId', params.createdByUserId);
  const qs = search.toString();
  return qs ? `?${qs}` : '';
}

/** Project-scoped automations (GET/POST /projects/{id}/automations) — reuses
 * AutomationsController's single-item endpoints (GET/PUT/DELETE/enable/disable/duplicate/runs/
 * retry/test) for everything else, since those already authorize per-automation regardless of
 * which project the caller navigated from. */
export const automationsApi = {
  listForProject: (projectId: string, params?: AutomationQueryParams) =>
    httpClient.get<Automation[]>(`/projects/${projectId}/automations${buildQuery(params)}`),
  createForProject: (projectId: string, request: SaveAutomationRequest) =>
    httpClient.post<Automation>(`/projects/${projectId}/automations`, request),

  getById: (id: string) => httpClient.get<Automation>(`/automations/${id}`),
  update: (id: string, request: SaveAutomationRequest) => httpClient.put<Automation>(`/automations/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/automations/${id}`),
  enable: (id: string) => httpClient.post<Automation>(`/automations/${id}/enable`),
  disable: (id: string) => httpClient.post<Automation>(`/automations/${id}/disable`),
  duplicate: (id: string) => httpClient.post<Automation>(`/automations/${id}/duplicate`),
  runs: (id: string, page = 1, pageSize = 25) =>
    httpClient.get<AutomationExecutionList>(`/automations/${id}/runs?page=${page}&pageSize=${pageSize}`),
  retryRun: (id: string, runId: string) => httpClient.post<Automation>(`/automations/${id}/runs/${runId}/retry`),
  test: (id: string, sampleEntityId: string) =>
    httpClient.post<AutomationTestResult>(`/automations/${id}/test`, { sampleEntityId }),
};

/** Admin -> Automations — global (ProjectId-null) automations only, Administrator-only per
 * AdminAutomationsController's own [Authorize]. */
export const adminAutomationsApi = {
  listAll: (params?: AutomationQueryParams) => httpClient.get<Automation[]>(`/admin/automations${buildQuery(params)}`),
  create: (request: SaveAutomationRequest) => httpClient.post<Automation>('/admin/automations', request),
};
