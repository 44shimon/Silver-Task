import { Flag } from 'lucide-react';
import type { PriorityCount } from '@/types/dashboard';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import { DashboardWidget } from './DashboardWidget';
import { BreakdownList } from './BreakdownList';

interface PriorityBreakdownWidgetProps {
  breakdown: PriorityCount[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function PriorityBreakdownWidget({ breakdown, isLoading, isError, onRetry }: PriorityBreakdownWidgetProps) {
  return (
    <DashboardWidget
      title="My Open Tasks by Priority"
      icon={<Flag size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={breakdown.length === 0}
      emptyTitle="No open tasks"
    >
      <BreakdownList
        rows={breakdown.map((row) => ({ key: row.priority, count: row.count, badge: <PriorityBadge priority={row.priority} /> }))}
      />
    </DashboardWidget>
  );
}
