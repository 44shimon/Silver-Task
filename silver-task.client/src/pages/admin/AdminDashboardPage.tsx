import { useAdminStats } from '@/hooks/useAdminStats';
import type { AdminStats } from '@/types/admin';
import './AdminDashboardPage.css';

const CARDS: { label: string; key: keyof AdminStats }[] = [
  { label: 'Total Users', key: 'totalUsers' },
  { label: 'Active Users', key: 'activeUsers' },
  { label: 'Total Projects', key: 'totalProjects' },
  { label: 'Total Tasks', key: 'totalTasks' },
  { label: 'Open Tasks', key: 'openTasks' },
  { label: 'Completed Tasks', key: 'completedTasks' },
];

export function AdminDashboardPage() {
  const { data: stats, isLoading, isError } = useAdminStats();

  if (isLoading) {
    return <p>Loading system statistics...</p>;
  }

  if (isError || !stats) {
    return <p>System statistics could not be loaded.</p>;
  }

  return (
    <div className="admin-dashboard">
      {CARDS.map((card) => (
        <div className="admin-dashboard__card" key={card.label}>
          <span className="admin-dashboard__value">{stats[card.key]}</span>
          <span className="admin-dashboard__label">{card.label}</span>
        </div>
      ))}
    </div>
  );
}
