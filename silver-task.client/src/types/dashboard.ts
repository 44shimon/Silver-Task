import type { Task, TaskPriority, TaskStatus } from './task';

export interface TaskSummary {
  myTasksCount: number;
  dueTodayCount: number;
  dueThisWeekCount: number;
  overdueCount: number;
  completedThisWeekCount: number;
}

export interface WeekSummary {
  assignedCount: number;
  completedCount: number;
  remainingCount: number;
  overdueCount: number;
  /** 0-1, not a percentage — the widget formats it. */
  completionRate: number;
}

export interface ProjectProgress {
  projectId: string;
  projectName: string;
  isArchived: boolean;
  openCount: number;
  completedCount: number;
  percentComplete: number;
}

export interface PriorityCount {
  priority: TaskPriority;
  count: number;
}

export interface StatusCount {
  status: TaskStatus;
  count: number;
}

export interface ActivityFeedItem {
  id: string;
  taskId: string;
  taskTitle: string;
  projectId: string;
  projectName: string;
  userName: string | null;
  action: string;
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}

export interface WorkloadEntry {
  userId: string;
  userName: string;
  openTaskCount: number;
}

export interface TeamWorkload {
  entries: WorkloadEntry[];
}

export interface DashboardData {
  taskSummary: TaskSummary;
  weekSummary: WeekSummary;
  overdueTasks: Task[];
  dueTodayTasks: Task[];
  upcomingTasks: Task[];
  recentlyCompletedTasks: Task[];
  myProjects: ProjectProgress[];
  priorityBreakdown: PriorityCount[];
  statusBreakdown: StatusCount[];
  recentActivity: ActivityFeedItem[];
}

export type UpcomingRange = 'today' | 'tomorrow' | '7days' | '30days';
export type StatsRange = 'today' | 'week' | 'month';

/** The dashboard's known widget ids — drives both the default layout and the customize panel.
 * A new widget added later just needs an entry here + in DEFAULT_WIDGET_ORDER
 * (components/dashboard/dashboardWidgets.ts), not a schema/migration change (DashboardLayout is
 * stored as an opaque JSON blob server-side). */
export type DashboardWidgetId =
  | 'taskSummary'
  | 'overdue'
  | 'dueToday'
  | 'upcoming'
  | 'recentlyCompleted'
  | 'myProjects'
  | 'priorityBreakdown'
  | 'statusBreakdown'
  | 'notifications'
  | 'recentFiles'
  | 'recentActivity'
  | 'teamWorkload'
  | 'adminOverview'
  | 'weekSummary'
  | 'reportsSummary'
  | 'workflow';

export interface DashboardLayout {
  visibleWidgets: DashboardWidgetId[];
  order: DashboardWidgetId[];
}

/** Phase 39 — Blocked/Ready/Due Today over the caller's own open assigned tasks. */
export interface WorkflowSummary {
  blocked: number;
  ready: number;
  dueToday: number;
}
