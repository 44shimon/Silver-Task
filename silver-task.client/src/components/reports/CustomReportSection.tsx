import { useState } from 'react';
import { Wrench } from 'lucide-react';
import { useCustomReport } from '@/hooks/useReports';
import { DashboardWidget } from '@/components/dashboard/DashboardWidget';
import { BreakdownList } from '@/components/dashboard/BreakdownList';
import type { ReportFilters, ReportGroupByField } from '@/types/reports';

interface CustomReportSectionProps {
  filters: ReportFilters;
  groupBy: ReportGroupByField;
  onGroupByChange: (groupBy: ReportGroupByField) => void;
}

const GROUP_BY_OPTIONS: ReportGroupByField[] = ['Project', 'Status', 'Priority', 'Assignee'];

// The minimal Report Builder (spec's own "keep this manageable, do not build a full BI platform"
// instruction): Data source is always Tasks, Metric is always Count, Group By is one of four
// closed fields — no free-text/expression parser exists anywhere in this component, so there is
// no code path capable of accepting arbitrary SQL/C#/JavaScript even in principle.
export function CustomReportSection({ filters, groupBy, onGroupByChange }: CustomReportSectionProps) {
  const [pendingGroupBy, setPendingGroupBy] = useState(groupBy);
  const report = useCustomReport(filters, groupBy);

  return (
    <DashboardWidget
      title="Custom Report"
      icon={<Wrench size={14} />}
      isLoading={report.isLoading}
      isError={report.isError}
      onRetry={() => report.refetch()}
      isEmpty={report.data?.length === 0}
      emptyTitle="No data available"
      headerAction={
        <div className="report-builder">
          <select
            className="dashboard-widget__range-select"
            value={pendingGroupBy}
            onChange={(e) => setPendingGroupBy(e.target.value as ReportGroupByField)}
            aria-label="Group by"
          >
            {GROUP_BY_OPTIONS.map((f) => (
              <option key={f} value={f}>
                Group by {f}
              </option>
            ))}
          </select>
          <button type="button" onClick={() => onGroupByChange(pendingGroupBy)} disabled={pendingGroupBy === groupBy}>
            Preview
          </button>
        </div>
      }
    >
      {report.data && report.data.length > 0 && (
        <BreakdownList rows={report.data.map((row) => ({ key: row.label, count: row.count, badge: <span>{row.label}</span> }))} />
      )}
    </DashboardWidget>
  );
}
