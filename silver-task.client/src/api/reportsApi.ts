import { API_BASE, httpClient } from './httpClient';
import type {
  AdminSystemReport,
  AutomationReport,
  BlockedTaskReport,
  CompletionTimeReport,
  CustomFieldSummaryReport,
  DependencyReport,
  ExportFormat,
  FileReport,
  LabeledCount,
  LongestDependencyChainReport,
  NotificationReport,
  OldTaskReport,
  OverdueReport,
  ProjectProgressReport,
  ReportFilters,
  ReportGroupByField,
  ReportType,
  SavedReport,
  SaveReportRequest,
  TaskAgeReport,
  TaskSummaryReport,
  TemplateUsageReport,
  TrendReport,
  UserWorkloadReport,
  WorkflowBottlenecksReport,
} from '@/types/reports';

function buildQuery(filter: ReportFilters, extra?: Record<string, string | number | undefined>): string {
  const params = new URLSearchParams();
  if (filter.dateRange) params.set('dateRange', filter.dateRange);
  if (filter.startDate) params.set('startDate', filter.startDate);
  if (filter.endDate) params.set('endDate', filter.endDate);
  if (filter.projectId) params.set('projectId', filter.projectId);
  if (filter.userId) params.set('userId', filter.userId);
  if (filter.status) params.set('status', filter.status);
  if (filter.priority) params.set('priority', filter.priority);
  if (filter.labelId) params.set('labelId', filter.labelId);
  if (filter.search) params.set('search', filter.search);
  if (filter.page) params.set('page', String(filter.page));
  if (filter.pageSize) params.set('pageSize', String(filter.pageSize));
  if (extra) {
    for (const [key, value] of Object.entries(extra)) {
      if (value !== undefined) params.set(key, String(value));
    }
  }
  const qs = params.toString();
  return qs ? `?${qs}` : '';
}

/** Every endpoint resolves the caller from the auth cookie server-side — filters only ever
 * narrow within what the caller can already see (see ReportsController's own doc comment); there
 * is no way to request another user's report data through this API. */
export const reportsApi = {
  taskSummary: (filter: ReportFilters) => httpClient.get<TaskSummaryReport>(`/reports/task-summary${buildQuery(filter)}`),
  completionTrend: (filter: ReportFilters) => httpClient.get<TrendReport>(`/reports/completion-trend${buildQuery(filter)}`),
  creationTrend: (filter: ReportFilters) => httpClient.get<TrendReport>(`/reports/creation-trend${buildQuery(filter)}`),
  overdue: (filter: ReportFilters) => httpClient.get<OverdueReport>(`/reports/overdue${buildQuery(filter)}`),
  overdueTrend: (filter: ReportFilters) => httpClient.get<TrendReport>(`/reports/overdue-trend${buildQuery(filter)}`),
  projectProgress: (filter: ReportFilters) => httpClient.get<ProjectProgressReport>(`/reports/project-progress${buildQuery(filter)}`),
  workload: (filter: ReportFilters) => httpClient.get<UserWorkloadReport>(`/reports/workload${buildQuery(filter)}`),
  taskAge: (filter: ReportFilters) => httpClient.get<TaskAgeReport>(`/reports/task-age${buildQuery(filter)}`),
  oldTasks: (filter: ReportFilters, thresholdDays: number) =>
    httpClient.get<OldTaskReport>(`/reports/old-tasks${buildQuery(filter, { thresholdDays })}`),
  completionTime: (filter: ReportFilters) => httpClient.get<CompletionTimeReport>(`/reports/completion-time${buildQuery(filter)}`),
  automations: (filter: ReportFilters) => httpClient.get<AutomationReport>(`/reports/automations${buildQuery(filter)}`),
  notifications: () => httpClient.get<NotificationReport>('/reports/notifications'),
  files: (filter: ReportFilters) => httpClient.get<FileReport>(`/reports/files${buildQuery(filter)}`),
  adminSystem: () => httpClient.get<AdminSystemReport>('/reports/admin-system'),
  custom: (filter: ReportFilters, groupBy: ReportGroupByField) =>
    httpClient.get<LabeledCount[]>(`/reports/custom${buildQuery(filter, { groupBy })}`),
  dependencies: (filter: ReportFilters) => httpClient.get<DependencyReport>(`/reports/dependencies${buildQuery(filter)}`),
  blockedTasks: (filter: ReportFilters) => httpClient.get<BlockedTaskReport>(`/reports/blocked-tasks${buildQuery(filter)}`),
  bottlenecks: (filter: ReportFilters) => httpClient.get<WorkflowBottlenecksReport>(`/reports/bottlenecks${buildQuery(filter)}`),
  dependencyChain: (projectId: string) =>
    httpClient.get<LongestDependencyChainReport>(`/reports/dependency-chain?projectId=${projectId}`),
  templateUsage: () => httpClient.get<TemplateUsageReport>('/reports/template-usage'),
  customFieldSummary: (customFieldId: string) =>
    httpClient.get<CustomFieldSummaryReport>(`/reports/custom-field-summary?customFieldId=${customFieldId}`),
  /** Not a fetch — builds a direct, same-origin download URL for an <a>/window.open, same
   * pattern as attachmentsApi.downloadUrl. */
  exportUrl: (reportType: ReportType, filter: ReportFilters, format: ExportFormat, extra?: Record<string, string | number | undefined>) =>
    `${API_BASE}/reports/export${buildQuery(filter, { reportType, format, ...extra })}`,
};

export const savedReportsApi = {
  list: () => httpClient.get<SavedReport[]>('/saved-reports'),
  create: (request: SaveReportRequest) => httpClient.post<SavedReport>('/saved-reports', request),
  update: (id: string, request: SaveReportRequest) => httpClient.put<SavedReport>(`/saved-reports/${id}`, request),
  remove: (id: string) => httpClient.delete<void>(`/saved-reports/${id}`),
  duplicate: (id: string) => httpClient.post<SavedReport>(`/saved-reports/${id}/duplicate`),
  share: (id: string, email: string) => httpClient.post<void>(`/saved-reports/${id}/share`, { email }),
  unshare: (id: string, userId: string) => httpClient.delete<void>(`/saved-reports/${id}/share/${userId}`),
  favorite: (id: string) => httpClient.post<void>(`/saved-reports/${id}/favorite`),
  unfavorite: (id: string) => httpClient.delete<void>(`/saved-reports/${id}/favorite`),
  execute: <T>(id: string) => httpClient.get<T>(`/saved-reports/${id}/execute`),
};
