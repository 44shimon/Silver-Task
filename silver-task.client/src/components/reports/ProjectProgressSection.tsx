import { FolderKanban } from 'lucide-react';
import { useProjectProgressReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { TrendChart } from './TrendChart';
import type { ProjectProgressRow, ReportFilters } from '@/types/reports';

interface ProjectProgressSectionProps {
  filters: ReportFilters;
}

const HEALTH_LABELS: Record<ProjectProgressRow['health'], string> = {
  Healthy: 'Healthy',
  AtRisk: 'At Risk',
  Overdue: 'Overdue',
};

// Health is derived from two objective facts only (see ReportingService's own doc comment on
// ProjectProgressReportRowDto): Overdue means at least one open task is past its due date; AtRisk
// means nothing is overdue yet but something is due within 3 days; Healthy is everything else.
export function ProjectProgressSection({ filters }: ProjectProgressSectionProps) {
  const report = useProjectProgressReport(filters);

  return (
    <div className="report-section">
      <DashboardWidget
        title="Project Progress"
        icon={<FolderKanban size={14} />}
        isLoading={report.isLoading}
        isError={report.isError}
        onRetry={() => report.refetch()}
        isEmpty={report.data?.projects.length === 0}
        emptyTitle="No projects to report on"
      >
        {report.data && report.data.projects.length > 0 && (
          <table className="report-table">
            <thead>
              <tr>
                <th scope="col">Project</th>
                <th scope="col">Tasks</th>
                <th scope="col">Complete</th>
                <th scope="col">Progress</th>
                <th scope="col">Overdue</th>
                <th scope="col">Health</th>
              </tr>
            </thead>
            <tbody>
              {report.data.projects.map((row) => (
                <tr key={row.projectId}>
                  <td>{row.projectName}</td>
                  <td>{row.taskCount}</td>
                  <td>{row.completedCount}</td>
                  <td>{row.percentComplete}%</td>
                  <td>{row.overdueCount}</td>
                  <td>
                    <span className={`report-health-badge report-health-badge--${row.health.toLowerCase()}`}>
                      {HEALTH_LABELS[row.health]}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </DashboardWidget>

      {report.data?.completionTrend && (
        <DashboardWidget title="Project Completion Trend" isLoading={false} isError={false}>
          <TrendChart points={report.data.completionTrend.points} label="Percent complete" valueSuffix="%" />
        </DashboardWidget>
      )}
    </div>
  );
}
