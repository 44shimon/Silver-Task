import { GitBranch } from 'lucide-react';
import { useWorkflowSummary } from '@/hooks/useDashboard';
import { DashboardWidget } from './DashboardWidget';
import { StatCard } from './StatCard';
import './WorkflowWidget.css';

// Phase 39 — Blocked/Ready/Due Today over the caller's own open assigned tasks. Clicking a stat
// opens the existing My Tasks list pre-filtered (same ?quickFilter=/?dependencyState= seeding
// pattern the rest of the dashboard's stat cards already use) — never a duplicate task list view.
export function WorkflowWidget() {
  const { data, isLoading, isError, refetch } = useWorkflowSummary();

  return (
    <DashboardWidget title="Workflow" icon={<GitBranch size={14} />} isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
      {data && (
        <div className="workflow-widget__stats">
          <StatCard label="Blocked" value={data.blocked} to="/my-tasks?dependencyState=blocked" tone="urgent" />
          <StatCard label="Ready" value={data.ready} to="/my-tasks?dependencyState=notBlocked" />
          <StatCard label="Due Today" value={data.dueToday} to="/my-tasks?quickFilter=dueToday" />
        </div>
      )}
    </DashboardWidget>
  );
}
