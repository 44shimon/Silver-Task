import type { TaskPriority, TaskStatus } from './task';
import type { StatusCount, PriorityCount } from './dashboard';

export type ReportDateRangeKey =
  | 'today'
  | 'yesterday'
  | 'thisWeek'
  | 'lastWeek'
  | 'thisMonth'
  | 'lastMonth'
  | 'thisQuarter'
  | 'thisYear'
  | 'custom';

export const DATE_RANGE_OPTIONS: { value: ReportDateRangeKey; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'thisWeek', label: 'This Week' },
  { value: 'lastWeek', label: 'Last Week' },
  { value: 'thisMonth', label: 'This Month' },
  { value: 'lastMonth', label: 'Last Month' },
  { value: 'thisQuarter', label: 'This Quarter' },
  { value: 'thisYear', label: 'This Year' },
  { value: 'custom', label: 'Custom Range' },
];

/** The one closed filter set every report query shares — mirrors
 * Silver-Task.Server/Models/DTOs/Reports/ReportFilterRequest.cs exactly. */
export interface ReportFilters {
  dateRange?: ReportDateRangeKey;
  startDate?: string;
  endDate?: string;
  projectId?: string;
  userId?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  labelId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface TaskSummaryReport {
  total: number;
  completed: number;
  open: number;
  overdue: number;
  completionRate: number;
  byStatus: StatusCount[];
  byPriority: PriorityCount[];
}

export interface TrendPoint {
  label: string;
  periodStart: string;
  count: number;
}

export interface TrendReport {
  granularity: 'day' | 'week' | 'month';
  points: TrendPoint[];
}

export interface OverdueTaskRow {
  taskId: string;
  taskTitle: string;
  projectId: string;
  projectName: string;
  assigneeName: string | null;
  dueDate: string;
  daysOverdue: number;
  priority: TaskPriority;
}

export interface OverdueReport {
  items: OverdueTaskRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProjectProgressRow {
  projectId: string;
  projectName: string;
  taskCount: number;
  completedCount: number;
  percentComplete: number;
  overdueCount: number;
  health: 'Healthy' | 'AtRisk' | 'Overdue';
}

export interface ProjectProgressReport {
  projects: ProjectProgressRow[];
  completionTrend: TrendReport | null;
}

export interface UserWorkloadRow {
  userId: string;
  userName: string;
  openCount: number;
  completedCount: number;
  overdueCount: number;
  completionRate: number;
}

export interface UserWorkloadReport {
  entries: UserWorkloadRow[];
}

export interface TaskAgeBucket {
  bucket: string;
  count: number;
}

export interface TaskAgeReport {
  buckets: TaskAgeBucket[];
  totalOpen: number;
}

export interface OldTaskRow {
  taskId: string;
  taskTitle: string;
  projectId: string;
  projectName: string;
  assigneeName: string | null;
  createdAt: string;
  ageDays: number;
}

export interface OldTaskReport {
  items: OldTaskRow[];
  totalCount: number;
  page: number;
  pageSize: number;
  thresholdDays: number;
}

export interface PriorityCompletionTime {
  priority: TaskPriority;
  averageDays: number | null;
  sampleSize: number;
}

/** Created -> Completed only — this app deliberately has no reliable "started" timestamp, so
 * Cycle Time is not implemented (see the Phase 38 final report). Never mix this with a
 * started-at-based calculation. */
export interface CompletionTimeReport {
  averageDays: number | null;
  sampleSize: number;
  byPriority: PriorityCompletionTime[];
}

export interface AutomationReportRow {
  automationId: string;
  name: string;
  triggerType: string;
  isActive: boolean;
  runCount: number;
  successCount: number;
  failedCount: number;
  lastRunAt: string | null;
}

export interface AutomationReport {
  automations: AutomationReportRow[];
}

export interface LabeledCount {
  label: string;
  count: number;
}

export interface NotificationReport {
  totalCount: number;
  unreadCount: number;
  byType: LabeledCount[];
  byPriority: LabeledCount[];
}

export interface FileReport {
  totalFiles: number;
  totalSizeBytes: number;
  filesInRange: number;
  byCategory: LabeledCount[];
}

export interface AdminSystemReport {
  totalUsers: number;
  activeUsers: number;
  totalProjects: number;
  totalTasks: number;
  completedTasks: number;
  overdueTasks: number;
  activeAutomations: number;
  totalNotifications: number;
  totalFiles: number;
}

export type ReportGroupByField = 'Project' | 'Status' | 'Priority' | 'Assignee';

export type ReportType =
  | 'TaskSummary'
  | 'CompletionTrend'
  | 'CreationTrend'
  | 'Overdue'
  | 'OverdueTrend'
  | 'ProjectProgress'
  | 'Workload'
  | 'UserCompletion'
  | 'TaskAge'
  | 'OldTasks'
  | 'CompletionTime'
  | 'Custom';

export const REPORT_TYPE_LABELS: Record<ReportType, string> = {
  TaskSummary: 'Task Summary',
  CompletionTrend: 'Completion Trend',
  CreationTrend: 'Creation Trend',
  Overdue: 'Overdue Tasks',
  OverdueTrend: 'Overdue Trend',
  ProjectProgress: 'Project Progress',
  Workload: 'Team Workload',
  UserCompletion: 'User Completion',
  TaskAge: 'Task Age',
  OldTasks: 'Old Tasks',
  CompletionTime: 'Completion Time',
  Custom: 'Custom Report',
};

/** Deserialized shape of a SavedReport's Configuration JSON — mirrors
 * Silver-Task.Server/Models/DTOs/Reports/ReportConfiguration.cs. Only closed, validated fields;
 * never executable code (see that file's own doc comment). */
export interface ReportConfiguration extends ReportFilters {
  reportType: ReportType;
  groupBy?: ReportGroupByField;
}

export interface SharedUser {
  userId: string;
  name: string;
}

export interface SavedReport {
  id: string;
  name: string;
  description: string | null;
  createdByUserId: string;
  createdByName: string;
  projectId: string | null;
  projectName: string | null;
  reportType: string;
  configuration: string;
  isOwnedByMe: boolean;
  isFavorite: boolean;
  sharedWith: SharedUser[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface SaveReportRequest {
  name: string;
  description?: string;
  projectId?: string;
  configuration: string;
}

export type ExportFormat = 'csv' | 'excel' | 'pdf';
