import { ShieldCheck } from 'lucide-react';
import { useAdminStats } from '@/hooks/useAdminStats';
import type { AdminStats } from '@/types/admin';
import { DashboardWidget } from './DashboardWidget';
import { StatCard } from './StatCard';
import './TaskSummaryWidget.css';

const CARDS: { label: string; key: keyof AdminStats }[] = [
  { label: 'Users', key: 'totalUsers' },
  { label: 'Projects', key: 'totalProjects' },
  { label: 'Open Tasks', key: 'openTasks' },
  { label: 'Completed Today', key: 'completedToday' },
  { label: 'Overdue Tasks', key: 'overdueTasks' },
];

// Only rendered for Administrators — see DashboardPage's own permission check (real
// Permissions.AdministrationAccess check, not a role-name string comparison) and note the
// backing endpoint (GET /api/admin/stats) is independently gated server-side by
// [Authorize(Roles=Administrator)] regardless of what the frontend shows/hides.
export function AdminOverviewWidget() {
  const { data: stats, isLoading, isError, refetch } = useAdminStats();

  return (
    <DashboardWidget title="System Overview" icon={<ShieldCheck size={14} />} isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
      {stats && (
        <div className="task-summary-widget">
          {CARDS.map((card) => (
            <StatCard key={card.label} label={card.label} value={stats[card.key]} tone={card.key === 'overdueTasks' ? 'urgent' : 'default'} />
          ))}
        </div>
      )}
    </DashboardWidget>
  );
}
