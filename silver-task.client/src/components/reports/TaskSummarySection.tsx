import { useState } from 'react';
import { ListChecks } from 'lucide-react';
import { useCompletionTrendReport, useCreationTrendReport, useTaskSummaryReport } from '@/hooks/useReports';
import { StatCard } from '@/components/dashboard/StatCard';
import { BreakdownList } from '@/components/dashboard/BreakdownList';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import { StatusBadge } from '@/components/spreadsheet/StatusBadge';
import { TrendChart } from './TrendChart';
import type { ReportFilters } from '@/types/reports';

interface TaskSummarySectionProps {
  filters: ReportFilters;
}

export function TaskSummarySection({ filters }: TaskSummarySectionProps) {
  const [trend, setTrend] = useState<'completion' | 'creation'>('completion');
  const summary = useTaskSummaryReport(filters);
  const completionTrend = useCompletionTrendReport(filters);
  const creationTrend = useCreationTrendReport(filters);
  const activeTrend = trend === 'completion' ? completionTrend : creationTrend;

  return (
    <div className="report-section">
      <DashboardWidget
        title="Task Summary"
        icon={<ListChecks size={14} />}
        isLoading={summary.isLoading}
        isError={summary.isError}
        onRetry={() => summary.refetch()}
        isEmpty={summary.data?.total === 0}
        emptyTitle="No data available"
        emptyMessage="No tasks were created in the selected range and filters."
      >
        {summary.data && (
          <>
            <div className="report-section__stats">
              <StatCard label="Total" value={summary.data.total} />
              <StatCard label="Completed" value={summary.data.completed} />
              <StatCard label="Open" value={summary.data.open} />
              <StatCard label="Overdue" value={summary.data.overdue} tone="urgent" />
            </div>
            <p className="report-section__rate">Completion Rate: {Math.round(summary.data.completionRate * 100)}%</p>

            <div className="report-section__breakdowns">
              <div>
                <h3 className="report-section__subheading">By Status</h3>
                <BreakdownList
                  rows={summary.data.byStatus.map((r) => ({ key: r.status, count: r.count, badge: <StatusBadge status={r.status} /> }))}
                />
              </div>
              <div>
                <h3 className="report-section__subheading">By Priority</h3>
                <BreakdownList
                  rows={summary.data.byPriority.map((r) => ({ key: r.priority, count: r.count, badge: <PriorityBadge priority={r.priority} /> }))}
                />
              </div>
            </div>
          </>
        )}
      </DashboardWidget>

      <DashboardWidget
        title={trend === 'completion' ? 'Tasks Completed Over Time' : 'Tasks Created Over Time'}
        isLoading={activeTrend.isLoading}
        isError={activeTrend.isError}
        onRetry={() => activeTrend.refetch()}
        isEmpty={activeTrend.data?.points.every((p) => p.count === 0) ?? false}
        emptyTitle="No data available"
        headerAction={
          <select
            className="dashboard-widget__range-select"
            value={trend}
            onChange={(e) => setTrend(e.target.value as 'completion' | 'creation')}
            aria-label="Trend type"
          >
            <option value="completion">Completed</option>
            <option value="creation">Created</option>
          </select>
        }
      >
        {activeTrend.data && <TrendChart points={activeTrend.data.points} label={trend === 'completion' ? 'Completed' : 'Created'} />}
      </DashboardWidget>
    </div>
  );
}
