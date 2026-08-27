import type { TaskSummary } from '@/types/dashboard';
import { StatCard } from './StatCard';
import './TaskSummaryWidget.css';

interface TaskSummaryWidgetProps {
  summary: TaskSummary;
}

// Clicking a card navigates to My Tasks pre-filtered — reuses the exact quick-filter query
// convention useMyTasksFilters already establishes (?quickFilter=...) rather than inventing a
// second filter mechanism for cards that happen to live on a different page.
export function TaskSummaryWidget({ summary }: TaskSummaryWidgetProps) {
  return (
    <div className="task-summary-widget">
      <StatCard label="My Tasks" value={summary.myTasksCount} to="/my-tasks" />
      <StatCard label="Due Today" value={summary.dueTodayCount} to="/my-tasks?quickFilter=dueToday" />
      <StatCard label="Due This Week" value={summary.dueThisWeekCount} to="/my-tasks?quickFilter=dueThisWeek" />
      <StatCard label="Overdue" value={summary.overdueCount} to="/my-tasks?quickFilter=overdue" tone="urgent" />
      <StatCard label="Completed This Week" value={summary.completedThisWeekCount} to="/my-tasks?quickFilter=completed" />
    </div>
  );
}
