import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reportsApi, savedReportsApi } from '@/api/reportsApi';
import type { ReportFilters, ReportGroupByField, SaveReportRequest } from '@/types/reports';

const savedReportsKey = ['saved-reports'] as const;

export function useTaskSummaryReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'task-summary', filter], queryFn: () => reportsApi.taskSummary(filter) });
}

export function useCompletionTrendReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'completion-trend', filter], queryFn: () => reportsApi.completionTrend(filter) });
}

export function useCreationTrendReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'creation-trend', filter], queryFn: () => reportsApi.creationTrend(filter) });
}

export function useOverdueReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'overdue', filter], queryFn: () => reportsApi.overdue(filter) });
}

export function useOverdueTrendReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'overdue-trend', filter], queryFn: () => reportsApi.overdueTrend(filter) });
}

export function useProjectProgressReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'project-progress', filter], queryFn: () => reportsApi.projectProgress(filter) });
}

export function useWorkloadReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'workload', filter], queryFn: () => reportsApi.workload(filter) });
}

export function useTaskAgeReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'task-age', filter], queryFn: () => reportsApi.taskAge(filter) });
}

export function useOldTasksReport(filter: ReportFilters, thresholdDays: number) {
  return useQuery({ queryKey: ['reports', 'old-tasks', filter, thresholdDays], queryFn: () => reportsApi.oldTasks(filter, thresholdDays) });
}

export function useCompletionTimeReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'completion-time', filter], queryFn: () => reportsApi.completionTime(filter) });
}

export function useAutomationReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'automations', filter], queryFn: () => reportsApi.automations(filter) });
}

export function useNotificationReport() {
  return useQuery({ queryKey: ['reports', 'notifications'], queryFn: reportsApi.notifications });
}

export function useFileReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'files', filter], queryFn: () => reportsApi.files(filter) });
}

export function useAdminSystemReport() {
  return useQuery({ queryKey: ['reports', 'admin-system'], queryFn: reportsApi.adminSystem });
}

export function useCustomReport(filter: ReportFilters, groupBy: ReportGroupByField) {
  return useQuery({ queryKey: ['reports', 'custom', filter, groupBy], queryFn: () => reportsApi.custom(filter, groupBy) });
}

export function useDependencyReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'dependencies', filter], queryFn: () => reportsApi.dependencies(filter) });
}

export function useBlockedTaskReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'blocked-tasks', filter], queryFn: () => reportsApi.blockedTasks(filter) });
}

export function useWorkflowBottlenecksReport(filter: ReportFilters) {
  return useQuery({ queryKey: ['reports', 'bottlenecks', filter], queryFn: () => reportsApi.bottlenecks(filter) });
}

export function useLongestDependencyChain(projectId: string | undefined) {
  return useQuery({
    queryKey: ['reports', 'dependency-chain', projectId ?? ''],
    queryFn: () => reportsApi.dependencyChain(projectId!),
    enabled: Boolean(projectId),
  });
}

export function useSavedReports() {
  return useQuery({ queryKey: savedReportsKey, queryFn: savedReportsApi.list });
}

export function useCreateSavedReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveReportRequest) => savedReportsApi.create(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}

export function useUpdateSavedReport(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SaveReportRequest) => savedReportsApi.update(id, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}

export function useDeleteSavedReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => savedReportsApi.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}

export function useDuplicateSavedReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => savedReportsApi.duplicate(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}

export function useShareSavedReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, email }: { id: string; email: string }) => savedReportsApi.share(id, email),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}

export function useUnshareSavedReport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, userId }: { id: string; userId: string }) => savedReportsApi.unshare(id, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}

export function useToggleSavedReportFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) =>
      favorite ? savedReportsApi.favorite(id) : savedReportsApi.unfavorite(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: savedReportsKey }),
  });
}
