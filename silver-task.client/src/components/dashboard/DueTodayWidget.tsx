import { CalendarClock } from 'lucide-react';
import type { Task } from '@/types/task';
import { DashboardWidget } from './DashboardWidget';
import { TaskPreviewList } from './TaskPreviewList';

interface DueTodayWidgetProps {
  tasks: Task[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function DueTodayWidget({ tasks, isLoading, isError, onRetry }: DueTodayWidgetProps) {
  return (
    <DashboardWidget
      title="Due Today"
      icon={<CalendarClock size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={tasks.length === 0}
      emptyTitle="Nothing due today"
      emptyMessage="Enjoy the breathing room."
    >
      <TaskPreviewList tasks={tasks} />
    </DashboardWidget>
  );
}
