import { useState } from 'react';
import { useCurrentUser } from '@/hooks/useAuth';
import { useUserPreferences, useUpdatePreferences } from '@/hooks/useUserSettings';
import { useDashboard, useTeamWorkload } from '@/hooks/useDashboard';
import { usePermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import type { DashboardLayout, DashboardWidgetId, StatsRange, UpcomingRange } from '@/types/dashboard';
import { DEFAULT_LAYOUT, parseDashboardLayout } from '@/components/dashboard/dashboardWidgets';
import { DashboardGreeting } from '@/components/dashboard/DashboardGreeting';
import { DashboardCustomizePanel } from '@/components/dashboard/DashboardCustomizePanel';
import { TaskSummaryWidget } from '@/components/dashboard/TaskSummaryWidget';
import { WeekSummaryWidget } from '@/components/dashboard/WeekSummaryWidget';
import { OverdueWidget } from '@/components/dashboard/OverdueWidget';
import { DueTodayWidget } from '@/components/dashboard/DueTodayWidget';
import { UpcomingWidget } from '@/components/dashboard/UpcomingWidget';
import { RecentlyCompletedWidget } from '@/components/dashboard/RecentlyCompletedWidget';
import { MyProjectsWidget } from '@/components/dashboard/MyProjectsWidget';
import { PriorityBreakdownWidget } from '@/components/dashboard/PriorityBreakdownWidget';
import { StatusBreakdownWidget } from '@/components/dashboard/StatusBreakdownWidget';
import { NotificationsWidget } from '@/components/dashboard/NotificationsWidget';
import { RecentFilesWidget } from '@/components/dashboard/RecentFilesWidget';
import { RecentActivityWidget } from '@/components/dashboard/RecentActivityWidget';
import { TeamWorkloadWidget } from '@/components/dashboard/TeamWorkloadWidget';
import { AdminOverviewWidget } from '@/components/dashboard/AdminOverviewWidget';
import { QuickActionsWidget } from '@/components/dashboard/QuickActionsWidget';
import './DashboardPage.css';

export function DashboardPage() {
  const { data: currentUser } = useCurrentUser();
  const { data: preferences, isLoading: preferencesLoading } = useUserPreferences();
  const updatePreferences = useUpdatePreferences();
  const { can } = usePermissions();
  const isAdmin = can(Permissions.AdministrationAccess);

  const [upcomingRange, setUpcomingRange] = useState<UpcomingRange>('7days');
  const [statsRange, setStatsRange] = useState<StatsRange>('week');
  const { data, isLoading, isError, refetch } = useDashboard(upcomingRange, statsRange);
  const teamWorkload = useTeamWorkload();
  const managesAnyProject = !!teamWorkload.data;

  // Local optimistic copy so checkbox/reorder clicks feel instant — reconciled with the server
  // via useUpdatePreferences the same "patch now, persist in the background" pattern every other
  // settings toggle in this app already uses (e.g. NotificationSettingsPage).
  const [localLayout, setLocalLayout] = useState<DashboardLayout | null>(null);
  const layout = localLayout ?? (preferencesLoading ? DEFAULT_LAYOUT : parseDashboardLayout(preferences?.dashboardLayout ?? null));

  function saveLayout(next: DashboardLayout) {
    setLocalLayout(next);
    if (!preferences) return;
    updatePreferences.mutate({ ...preferences, dashboardLayout: JSON.stringify(next) });
  }

  const visible = new Set(layout.visibleWidgets);
  const orderedVisible = layout.order.filter((id) => visible.has(id) && isWidgetAllowed(id, isAdmin, managesAnyProject));

  function isWidgetAllowed(id: DashboardWidgetId, admin: boolean, manages: boolean): boolean {
    if (id === 'adminOverview') return admin;
    if (id === 'teamWorkload') return manages;
    return true;
  }

  function renderWidget(id: DashboardWidgetId) {
    switch (id) {
      case 'taskSummary':
        return data && <TaskSummaryWidget summary={data.taskSummary} />;
      case 'weekSummary':
        return (
          <WeekSummaryWidget
            summary={data?.weekSummary ?? { assignedCount: 0, completedCount: 0, remainingCount: 0, overdueCount: 0, completionRate: 0 }}
            range={statsRange}
            onRangeChange={setStatsRange}
            isLoading={isLoading}
            isError={isError}
            onRetry={() => refetch()}
          />
        );
      case 'overdue':
        return <OverdueWidget tasks={data?.overdueTasks ?? []} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />;
      case 'dueToday':
        return <DueTodayWidget tasks={data?.dueTodayTasks ?? []} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />;
      case 'upcoming':
        return (
          <UpcomingWidget
            tasks={data?.upcomingTasks ?? []}
            range={upcomingRange}
            onRangeChange={setUpcomingRange}
            isLoading={isLoading}
            isError={isError}
            onRetry={() => refetch()}
          />
        );
      case 'recentlyCompleted':
        return <RecentlyCompletedWidget tasks={data?.recentlyCompletedTasks ?? []} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />;
      case 'myProjects':
        return <MyProjectsWidget projects={data?.myProjects ?? []} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />;
      case 'priorityBreakdown':
        return <PriorityBreakdownWidget breakdown={data?.priorityBreakdown ?? []} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />;
      case 'statusBreakdown':
        return <StatusBreakdownWidget breakdown={data?.statusBreakdown ?? []} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />;
      case 'notifications':
        return <NotificationsWidget />;
      case 'recentFiles':
        return <RecentFilesWidget />;
      case 'recentActivity':
        return <RecentActivityWidget />;
      case 'teamWorkload':
        return <TeamWorkloadWidget />;
      case 'adminOverview':
        return <AdminOverviewWidget />;
      default:
        return null;
    }
  }

  return (
    <div className="dashboard-page">
      <div className="dashboard-page__header">
        <DashboardGreeting name={currentUser?.name ?? ''} />
        <DashboardCustomizePanel layout={layout} onChange={saveLayout} isAdmin={isAdmin} managesAnyProject={managesAnyProject} />
      </div>

      <QuickActionsWidget />

      <div className="dashboard-page__grid">
        {orderedVisible.map((id) => (
          <div key={id} className={`dashboard-page__cell dashboard-page__cell--${id}`}>
            {renderWidget(id)}
          </div>
        ))}
      </div>

      {orderedVisible.length === 0 && (
        <p className="dashboard-page__empty">Every widget is hidden — use Customize Dashboard to bring some back.</p>
      )}
    </div>
  );
}
