import { ShieldCheck } from 'lucide-react';
import { useAdminSystemReport, useAutomationReport, useFileReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { StatCard } from '@/components/dashboard/StatCard';
import type { ReportFilters } from '@/types/reports';

interface AdminReportsSectionProps {
  filters: ReportFilters;
}

// Administrator-only, system-wide — every metric here is something the app already tracks
// elsewhere; no passwords/credentials/tokens are ever part of this view (see
// AdminSystemReportDto's own doc comment). Route access is additionally gated in ReportsPage via
// User.role === 'Administrator', mirroring the backend's own [Authorize(Roles = "Administrator")]
// on GET /api/reports/admin-system.
export function AdminReportsSection({ filters }: AdminReportsSectionProps) {
  const system = useAdminSystemReport();
  const automations = useAutomationReport(filters);
  const files = useFileReport(filters);

  return (
    <div className="report-section">
      <DashboardWidget
        title="System Overview"
        icon={<ShieldCheck size={14} />}
        isLoading={system.isLoading}
        isError={system.isError}
        onRetry={() => system.refetch()}
      >
        {system.data && (
          <div className="report-section__stats">
            <StatCard label="Users" value={system.data.totalUsers} />
            <StatCard label="Active Users" value={system.data.activeUsers} />
            <StatCard label="Projects" value={system.data.totalProjects} />
            <StatCard label="Tasks" value={system.data.totalTasks} />
            <StatCard label="Completed" value={system.data.completedTasks} />
            <StatCard label="Overdue" value={system.data.overdueTasks} tone="urgent" />
            <StatCard label="Active Automations" value={system.data.activeAutomations} />
            <StatCard label="Notifications" value={system.data.totalNotifications} />
            <StatCard label="Files" value={system.data.totalFiles} />
          </div>
        )}
      </DashboardWidget>

      <DashboardWidget
        title="Automation Runs"
        isLoading={automations.isLoading}
        isError={automations.isError}
        onRetry={() => automations.refetch()}
        isEmpty={automations.data?.automations.length === 0}
        emptyTitle="No automations"
      >
        {automations.data && automations.data.automations.length > 0 && (
          <table className="report-table">
            <thead>
              <tr>
                <th scope="col">Automation</th>
                <th scope="col">Trigger</th>
                <th scope="col">Runs</th>
                <th scope="col">Success</th>
                <th scope="col">Failed</th>
                <th scope="col">Last Run</th>
              </tr>
            </thead>
            <tbody>
              {automations.data.automations.map((row) => (
                <tr key={row.automationId}>
                  <td>{row.name}</td>
                  <td>{row.triggerType}</td>
                  <td>{row.runCount}</td>
                  <td>{row.successCount}</td>
                  <td>{row.failedCount}</td>
                  <td>{row.lastRunAt ? row.lastRunAt.slice(0, 10) : 'Never'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </DashboardWidget>

      <DashboardWidget title="Files" isLoading={files.isLoading} isError={files.isError} onRetry={() => files.refetch()}>
        {files.data && (
          <div className="report-section__stats">
            <StatCard label="Total Files" value={files.data.totalFiles} />
            <StatCard label="In Range" value={files.data.filesInRange} />
            <StatCard label="Total Size (MB)" value={Math.round(files.data.totalSizeBytes / 1024 / 1024)} />
          </div>
        )}
      </DashboardWidget>
    </div>
  );
}
