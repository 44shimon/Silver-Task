import { CalendarDays } from 'lucide-react';
import type { Task } from '@/types/task';
import type { UpcomingRange } from '@/types/dashboard';
import { DashboardWidget } from './DashboardWidget';
import { TaskPreviewList } from './TaskPreviewList';

interface UpcomingWidgetProps {
  tasks: Task[];
  range: UpcomingRange;
  onRangeChange: (range: UpcomingRange) => void;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

const RANGE_OPTIONS: { id: UpcomingRange; label: string }[] = [
  { id: 'today', label: 'Today' },
  { id: 'tomorrow', label: 'Tomorrow' },
  { id: '7days', label: 'Next 7 days' },
  { id: '30days', label: 'Next 30 days' },
];

export function UpcomingWidget({ tasks, range, onRangeChange, isLoading, isError, onRetry }: UpcomingWidgetProps) {
  return (
    <DashboardWidget
      title="Upcoming"
      icon={<CalendarDays size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={tasks.length === 0}
      emptyTitle="Nothing coming up"
      emptyMessage="No tasks due in this range."
      headerAction={
        <select
          className="dashboard-widget__range-select"
          value={range}
          onChange={(e) => onRangeChange(e.target.value as UpcomingRange)}
          aria-label="Upcoming range"
        >
          {RANGE_OPTIONS.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      }
    >
      <TaskPreviewList tasks={tasks} />
    </DashboardWidget>
  );
}
