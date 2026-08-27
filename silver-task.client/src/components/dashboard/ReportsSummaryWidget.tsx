import { BarChart3 } from 'lucide-react';
import { useTaskSummaryReport } from '@/hooks/useReports';
import { DashboardWidget } from './DashboardWidget';
import { StatCard } from './StatCard';
import './ReportsSummaryWidget.css';

// A compact preview only (Total/Completed/Overdue this month + a link to the full Reports
// Center) — deliberately NOT the complete reporting UI duplicated onto the dashboard, per the
// spec's own "do not duplicate the complete reporting UI on the dashboard" instruction. Opt-in
// (not in DEFAULT_VISIBLE_WIDGETS) since most users don't need reporting data on their daily
// landing page.
export function ReportsSummaryWidget() {
  const { data, isLoading, isError, refetch } = useTaskSummaryReport({ dateRange: 'thisMonth' });

  return (
    <DashboardWidget
      title="Reports Summary"
      icon={<BarChart3 size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      headerAction={
        <a href="/reports" className="dashboard-widget__range-select">
          View all
        </a>
      }
    >
      {data && (
        <div className="reports-summary-widget__stats">
          <StatCard label="Total (Month)" value={data.total} />
          <StatCard label="Completed" value={data.completed} />
          <StatCard label="Overdue" value={data.overdue} tone="urgent" />
        </div>
      )}
    </DashboardWidget>
  );
}
