import type { DashboardLayout, DashboardWidgetId } from '@/types/dashboard';

interface WidgetDefinition {
  id: DashboardWidgetId;
  label: string;
  /** Widgets requiring a specific permission/role are still enforced server-side independently
   * (Team Workload/Admin Overview's own endpoints) — this only controls whether the *option*
   * appears in the customize panel and default layout for a given viewer. */
  requiresAdmin?: boolean;
  requiresManagesAnyProject?: boolean;
}

// The full known widget set + sensible default layout (spec's own recommended default: Task
// Summary, My Tasks-adjacent widgets, My Projects, Notifications, Recent Activity). A new widget
// added later just needs an entry here — DashboardLayout itself is stored as an opaque JSON blob
// server-side (UserPreference.DashboardLayout), so this list is the only place that needs updating.
export const WIDGET_DEFINITIONS: WidgetDefinition[] = [
  { id: 'taskSummary', label: 'Task Summary' },
  { id: 'overdue', label: 'Overdue' },
  { id: 'dueToday', label: 'Due Today' },
  { id: 'upcoming', label: 'Upcoming' },
  { id: 'weekSummary', label: 'Weekly Completion' },
  { id: 'myProjects', label: 'My Projects' },
  { id: 'recentlyCompleted', label: 'Recently Completed' },
  { id: 'priorityBreakdown', label: 'Priority Breakdown' },
  { id: 'statusBreakdown', label: 'Status Breakdown' },
  { id: 'notifications', label: 'Notifications' },
  { id: 'recentFiles', label: 'Recent Files' },
  { id: 'recentActivity', label: 'Recent Activity' },
  { id: 'teamWorkload', label: 'Team Workload', requiresManagesAnyProject: true },
  { id: 'adminOverview', label: 'System Overview', requiresAdmin: true },
];

export const DEFAULT_VISIBLE_WIDGETS: DashboardWidgetId[] = [
  'taskSummary',
  'overdue',
  'dueToday',
  'upcoming',
  'myProjects',
  'notifications',
  'recentActivity',
];

export const DEFAULT_WIDGET_ORDER: DashboardWidgetId[] = WIDGET_DEFINITIONS.map((w) => w.id);

export const DEFAULT_LAYOUT: DashboardLayout = {
  visibleWidgets: DEFAULT_VISIBLE_WIDGETS,
  order: DEFAULT_WIDGET_ORDER,
};

export function parseDashboardLayout(raw: string | null | undefined): DashboardLayout {
  if (!raw) {
    return DEFAULT_LAYOUT;
  }
  try {
    const parsed = JSON.parse(raw) as Partial<DashboardLayout>;
    const knownIds = new Set(WIDGET_DEFINITIONS.map((w) => w.id));
    const order = (parsed.order ?? DEFAULT_WIDGET_ORDER).filter((id): id is DashboardWidgetId => knownIds.has(id));
    // Any widget introduced after this user last saved their layout still appears (appended at
    // the end) rather than silently disappearing forever.
    for (const id of DEFAULT_WIDGET_ORDER) {
      if (!order.includes(id)) {
        order.push(id);
      }
    }
    const visibleWidgets = (parsed.visibleWidgets ?? DEFAULT_VISIBLE_WIDGETS).filter((id): id is DashboardWidgetId => knownIds.has(id));
    return { visibleWidgets, order };
  } catch {
    return DEFAULT_LAYOUT;
  }
}
