import { Timer } from 'lucide-react';
import { useCompletionTimeReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import type { ReportFilters } from '@/types/reports';

interface CompletionTimeSectionProps {
  filters: ReportFilters;
}

// Created -> Completed only. This app has no reliable "started" timestamp (no field records when
// a task first moved to InProgress), so Cycle Time is deliberately NOT implemented anywhere in
// this report suite — see the Phase 38 final report. Never mix this metric with a
// started-at-based one.
export function CompletionTimeSection({ filters }: CompletionTimeSectionProps) {
  const report = useCompletionTimeReport(filters);

  return (
    <DashboardWidget
      title="Completion Time"
      icon={<Timer size={14} />}
      isLoading={report.isLoading}
      isError={report.isError}
      onRetry={() => report.refetch()}
      isEmpty={report.data?.sampleSize === 0}
      emptyTitle="No data available"
      emptyMessage="No tasks were completed within this filter, so an average completion time cannot be calculated."
    >
      {report.data && report.data.sampleSize > 0 && (
        <>
          <p className="report-section__rate">
            Overall Average: {report.data.averageDays?.toFixed(1)} days ({report.data.sampleSize} tasks)
          </p>
          <table className="report-table">
            <thead>
              <tr>
                <th scope="col">Priority</th>
                <th scope="col">Average Days</th>
                <th scope="col">Sample Size</th>
              </tr>
            </thead>
            <tbody>
              {report.data.byPriority.map((row) => (
                <tr key={row.priority}>
                  <td>
                    <PriorityBadge priority={row.priority} />
                  </td>
                  <td>{row.averageDays !== null ? row.averageDays.toFixed(1) : 'No data'}</td>
                  <td>{row.sampleSize}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </DashboardWidget>
  );
}
