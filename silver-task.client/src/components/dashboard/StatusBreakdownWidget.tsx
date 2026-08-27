import { ListChecks } from 'lucide-react';
import type { StatusCount } from '@/types/dashboard';
import { StatusBadge } from '@/components/spreadsheet/StatusBadge';
import { DashboardWidget } from './DashboardWidget';
import { BreakdownList } from './BreakdownList';

interface StatusBreakdownWidgetProps {
  breakdown: StatusCount[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function StatusBreakdownWidget({ breakdown, isLoading, isError, onRetry }: StatusBreakdownWidgetProps) {
  return (
    <DashboardWidget
      title="My Open Tasks by Status"
      icon={<ListChecks size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={breakdown.length === 0}
      emptyTitle="No open tasks"
    >
      <BreakdownList
        rows={breakdown.map((row) => ({ key: row.status, count: row.count, badge: <StatusBadge status={row.status} /> }))}
      />
    </DashboardWidget>
  );
}
