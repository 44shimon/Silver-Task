import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Hourglass } from 'lucide-react';
import { useOldTasksReport, useTaskAgeReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { BreakdownList } from '@/components/dashboard/BreakdownList';
import type { ReportFilters } from '@/types/reports';

interface TaskAgeSectionProps {
  filters: ReportFilters;
}

const THRESHOLD_OPTIONS = [7, 14, 30, 60, 90];

// Task Age and Old Tasks are kept visually together (same tab) but are two separate, independent
// calculations — Task Age buckets every open task by age; Old Tasks is a configurable-threshold
// filtered list. Neither ever modifies task data (both are read-only reports).
export function TaskAgeSection({ filters }: TaskAgeSectionProps) {
  const navigate = useNavigate();
  const [threshold, setThreshold] = useState(30);
  const ageReport = useTaskAgeReport(filters);
  const oldTasks = useOldTasksReport(filters, threshold);

  return (
    <div className="report-section">
      <DashboardWidget
        title="Task Age"
        icon={<Hourglass size={14} />}
        isLoading={ageReport.isLoading}
        isError={ageReport.isError}
        onRetry={() => ageReport.refetch()}
        isEmpty={ageReport.data?.totalOpen === 0}
        emptyTitle="No open tasks"
      >
        {ageReport.data && (
          <BreakdownList rows={ageReport.data.buckets.map((b) => ({ key: b.bucket, count: b.count, badge: <span>{b.bucket} days</span> }))} />
        )}
      </DashboardWidget>

      <DashboardWidget
        title="Old Tasks"
        isLoading={oldTasks.isLoading}
        isError={oldTasks.isError}
        onRetry={() => oldTasks.refetch()}
        isEmpty={oldTasks.data?.items.length === 0}
        emptyTitle="No old tasks"
        emptyMessage={`Nothing has been open for more than ${threshold} days.`}
        headerAction={
          <select
            className="dashboard-widget__range-select"
            value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
            aria-label="Age threshold"
          >
            {THRESHOLD_OPTIONS.map((t) => (
              <option key={t} value={t}>
                Over {t} days
              </option>
            ))}
          </select>
        }
      >
        {oldTasks.data && oldTasks.data.items.length > 0 && (
          <table className="report-table">
            <thead>
              <tr>
                <th scope="col">Task</th>
                <th scope="col">Project</th>
                <th scope="col">Assignee</th>
                <th scope="col">Created</th>
                <th scope="col">Age (days)</th>
              </tr>
            </thead>
            <tbody>
              {oldTasks.data.items.map((row) => (
                <tr
                  key={row.taskId}
                  className="report-table__row-link"
                  onClick={() => navigate(`/projects/${row.projectId}?task=${row.taskId}`)}
                >
                  <td>{row.taskTitle}</td>
                  <td>{row.projectName}</td>
                  <td>{row.assigneeName ?? 'Unassigned'}</td>
                  <td>{row.createdAt.slice(0, 10)}</td>
                  <td>{row.ageDays}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </DashboardWidget>
    </div>
  );
}
