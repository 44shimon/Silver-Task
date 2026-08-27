import { Users } from 'lucide-react';
import { useWorkloadReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import type { ReportFilters } from '@/types/reports';

interface WorkloadSectionProps {
  filters: ReportFilters;
}

// Also serves the spec's "User Completion Report" — same per-user Assigned/Completed/Overdue/
// Completion% shape (see ReportingService.GetWorkloadAsync's own doc comment on why this wasn't
// split into two near-identical queries).
export function WorkloadSection({ filters }: WorkloadSectionProps) {
  const report = useWorkloadReport(filters);

  return (
    <DashboardWidget
      title="Team Workload"
      icon={<Users size={14} />}
      isLoading={report.isLoading}
      isError={report.isError}
      onRetry={() => report.refetch()}
      isEmpty={report.data?.entries.length === 0}
      emptyTitle="No assigned tasks"
      emptyMessage="No tasks are currently assigned within this filter."
    >
      {report.data && report.data.entries.length > 0 && (
        <table className="report-table">
          <thead>
            <tr>
              <th scope="col">User</th>
              <th scope="col">Open</th>
              <th scope="col">Completed</th>
              <th scope="col">Overdue</th>
              <th scope="col">Completion Rate</th>
            </tr>
          </thead>
          <tbody>
            {report.data.entries.map((row) => (
              <tr key={row.userId}>
                <td>{row.userName}</td>
                <td>{row.openCount}</td>
                <td>{row.completedCount}</td>
                <td>{row.overdueCount}</td>
                <td>{Math.round(row.completionRate * 100)}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </DashboardWidget>
  );
}
