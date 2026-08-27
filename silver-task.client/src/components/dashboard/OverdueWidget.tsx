import { AlertTriangle } from 'lucide-react';
import type { Task } from '@/types/task';
import { DashboardWidget } from './DashboardWidget';
import { TaskPreviewList } from './TaskPreviewList';

interface OverdueWidgetProps {
  tasks: Task[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function OverdueWidget({ tasks, isLoading, isError, onRetry }: OverdueWidgetProps) {
  return (
    <DashboardWidget
      title="Overdue"
      icon={<AlertTriangle size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={tasks.length === 0}
      emptyTitle="No overdue tasks"
      emptyMessage="You're all caught up."
    >
      <TaskPreviewList tasks={tasks} />
    </DashboardWidget>
  );
}
