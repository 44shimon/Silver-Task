import { useNavigate } from 'react-router-dom';
import { GitBranch } from 'lucide-react';
import { useBlockedTaskReport, useDependencyReport, useLongestDependencyChain, useWorkflowBottlenecksReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { StatCard } from '@/components/dashboard/StatCard';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import type { ReportFilters } from '@/types/reports';

interface DependencySectionProps {
  filters: ReportFilters;
}

// "Circular Dependency Attempts" and true "Average Blocked Time" from the spec's suggested
// metric list are deliberately not shown here — see DependencyReportDto/BlockedTaskRowDto's own
// doc comments on the backend for why (a rejected circular-dependency request is never persisted
// anywhere, and this app doesn't track blocked/unblocked state transition timestamps). Longest
// Dependency Chain is offered instead of "Critical Path" — see its own doc comment.
export function DependencySection({ filters }: DependencySectionProps) {
  const navigate = useNavigate();
  const summary = useDependencyReport(filters);
  const blocked = useBlockedTaskReport(filters);
  const bottlenecks = useWorkflowBottlenecksReport(filters);
  const chain = useLongestDependencyChain(filters.projectId);

  return (
    <div className="report-section">
      <DashboardWidget
        title="Dependency Summary"
        icon={<GitBranch size={14} />}
        isLoading={summary.isLoading}
        isError={summary.isError}
        onRetry={() => summary.refetch()}
      >
        {summary.data && (
          <div className="report-section__stats">
            <StatCard label="Total Dependencies" value={summary.data.totalDependencies} />
            <StatCard label="Blocked Tasks" value={summary.data.blockedTasks} tone="urgent" />
            <StatCard label="Ready Tasks" value={summary.data.readyTasks} />
            <StatCard label="Blocking Others" value={summary.data.tasksBlockingOthers} />
            <StatCard label="Overrides" value={summary.data.dependencyOverrides} />
          </div>
        )}
      </DashboardWidget>

      <DashboardWidget
        title="Blocked Tasks"
        isLoading={blocked.isLoading}
        isError={blocked.isError}
        onRetry={() => blocked.refetch()}
        isEmpty={blocked.data?.items.length === 0}
        emptyTitle="No blocked tasks"
      >
        {blocked.data && blocked.data.items.length > 0 && (
          <table className="report-table">
            <thead>
              <tr>
                <th scope="col">Task</th>
                <th scope="col">Project</th>
                <th scope="col">Assignee</th>
                <th scope="col">Blocked By</th>
                <th scope="col">Blocked Since</th>
                <th scope="col">Priority</th>
              </tr>
            </thead>
            <tbody>
              {blocked.data.items.map((row) => (
                <tr key={row.taskId} className="report-table__row-link" onClick={() => navigate(`/projects/${row.projectId}?task=${row.taskId}`)}>
                  <td>{row.taskTitle}</td>
                  <td>{row.projectName}</td>
                  <td>{row.assigneeName ?? 'Unassigned'}</td>
                  <td>{row.blockedBy.join(', ')}</td>
                  <td>{row.blockedSince ? row.blockedSince.slice(0, 10) : '—'}</td>
                  <td>
                    <PriorityBadge priority={row.priority} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </DashboardWidget>

      <DashboardWidget
        title="Workflow Bottlenecks"
        isLoading={bottlenecks.isLoading}
        isError={bottlenecks.isError}
        onRetry={() => bottlenecks.refetch()}
        isEmpty={bottlenecks.data?.items.length === 0}
        emptyTitle="No bottlenecks"
        emptyMessage="No task currently blocks more than one other task."
      >
        {bottlenecks.data && bottlenecks.data.items.length > 0 && (
          <table className="report-table">
            <thead>
              <tr>
                <th scope="col">Task</th>
                <th scope="col">Project</th>
                <th scope="col">Blocks</th>
              </tr>
            </thead>
            <tbody>
              {bottlenecks.data.items.map((row) => (
                <tr key={row.taskId} className="report-table__row-link" onClick={() => navigate(`/projects/${row.projectId}?task=${row.taskId}`)}>
                  <td>{row.taskTitle}</td>
                  <td>{row.projectName}</td>
                  <td>{row.blocksCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </DashboardWidget>

      {filters.projectId && (
        <DashboardWidget
          title="Longest Dependency Chain"
          isLoading={chain.isLoading}
          isError={chain.isError}
          onRetry={() => chain.refetch()}
          isEmpty={chain.data?.chain.length === 0}
          emptyTitle="No dependency chain"
          emptyMessage="This project has no dependency relationships yet."
        >
          {chain.data && chain.data.chain.length > 0 && (
            <ol className="dependency-chain">
              {chain.data.chain.map((node) => (
                <li key={node.taskId} className={`dependency-chain__node dependency-chain__node--${node.status.toLowerCase()}`}>
                  <button type="button" onClick={() => navigate(`/projects/${chain.data!.projectId}?task=${node.taskId}`)}>
                    {node.taskTitle}
                  </button>
                </li>
              ))}
            </ol>
          )}
        </DashboardWidget>
      )}
      {!filters.projectId && (
        <p className="report-section__rate">Select a Project filter above to see its longest dependency chain.</p>
      )}
    </div>
  );
}
