import { useNavigate } from 'react-router-dom';
import { AlertTriangle } from 'lucide-react';
import { useOverdueReport, useOverdueTrendReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import { TrendChart } from './TrendChart';
import type { ReportFilters } from '@/types/reports';

interface OverdueSectionProps {
  filters: ReportFilters;
  onFiltersChange: (filters: ReportFilters) => void;
}

export function OverdueSection({ filters, onFiltersChange }: OverdueSectionProps) {
  const navigate = useNavigate();
  const report = useOverdueReport(filters);
  const trend = useOverdueTrendReport(filters);
  const page = filters.page ?? 1;
  const pageSize = filters.pageSize ?? 25;
  const totalPages = report.data ? Math.max(1, Math.ceil(report.data.totalCount / pageSize)) : 1;

  return (
    <div className="report-section">
      <DashboardWidget
        title="Overdue Tasks"
        icon={<AlertTriangle size={14} />}
        isLoading={report.isLoading}
        isError={report.isError}
        onRetry={() => report.refetch()}
        isEmpty={report.data?.items.length === 0}
        emptyTitle="No overdue tasks"
        emptyMessage="Nothing is currently overdue for this filter."
      >
        {report.data && report.data.items.length > 0 && (
          <>
            <table className="report-table">
              <thead>
                <tr>
                  <th scope="col">Task</th>
                  <th scope="col">Project</th>
                  <th scope="col">Assignee</th>
                  <th scope="col">Due Date</th>
                  <th scope="col">Days Overdue</th>
                  <th scope="col">Priority</th>
                </tr>
              </thead>
              <tbody>
                {report.data.items.map((row) => (
                  <tr
                    key={row.taskId}
                    className="report-table__row-link"
                    onClick={() => navigate(`/projects/${row.projectId}?task=${row.taskId}`)}
                  >
                    <td>{row.taskTitle}</td>
                    <td>{row.projectName}</td>
                    <td>{row.assigneeName ?? 'Unassigned'}</td>
                    <td>{row.dueDate}</td>
                    <td>{row.daysOverdue}</td>
                    <td>
                      <PriorityBadge priority={row.priority} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {totalPages > 1 && (
              <div className="report-section__pagination">
                <button type="button" disabled={page <= 1} onClick={() => onFiltersChange({ ...filters, page: page - 1 })}>
                  Previous
                </button>
                <span>
                  Page {page} of {totalPages}
                </span>
                <button type="button" disabled={page >= totalPages} onClick={() => onFiltersChange({ ...filters, page: page + 1 })}>
                  Next
                </button>
              </div>
            )}
          </>
        )}
      </DashboardWidget>

      <DashboardWidget
        title="Overdue Trend"
        isLoading={trend.isLoading}
        isError={trend.isError}
        onRetry={() => trend.refetch()}
        isEmpty={trend.data?.points.every((p) => p.count === 0) ?? false}
        emptyTitle="No data available"
      >
        {trend.data && <TrendChart points={trend.data.points} label="Overdue" />}
      </DashboardWidget>
    </div>
  );
}
