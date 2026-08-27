import { Users } from 'lucide-react';
import { useTeamWorkload } from '@/hooks/useDashboard';
import { DashboardWidget } from './DashboardWidget';
import './BreakdownList.css';

// Only rendered by DashboardPage when the query returns data at all (204/undefined means the
// caller manages no project) — see DashboardService.GetTeamWorkloadAsync's own doc comment on
// why this is enforced server-side, not just a frontend visibility check.
export function TeamWorkloadWidget() {
  const { data, isLoading, isError, refetch } = useTeamWorkload();
  const max = Math.max(1, ...(data?.entries.map((e) => e.openTaskCount) ?? [1]));

  return (
    <DashboardWidget
      title="Team Workload"
      icon={<Users size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      isEmpty={(data?.entries.length ?? 0) === 0}
      emptyTitle="No open tasks assigned"
    >
      <ul className="breakdown-list">
        {data?.entries.map((entry) => (
          <li key={entry.userId} className="breakdown-list__row">
            <span>{entry.userName}</span>
            <div className="breakdown-list__bar">
              <div className="breakdown-list__bar-fill" style={{ width: `${(entry.openTaskCount / max) * 100}%` }} />
            </div>
            <span className="breakdown-list__count">{entry.openTaskCount}</span>
          </li>
        ))}
      </ul>
    </DashboardWidget>
  );
}
