import { CheckCircle2 } from 'lucide-react';
import type { Task } from '@/types/task';
import { DashboardWidget } from './DashboardWidget';
import { TaskPreviewList } from './TaskPreviewList';

interface RecentlyCompletedWidgetProps {
  tasks: Task[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function RecentlyCompletedWidget({ tasks, isLoading, isError, onRetry }: RecentlyCompletedWidgetProps) {
  return (
    <DashboardWidget
      title="Recently Completed"
      icon={<CheckCircle2 size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={tasks.length === 0}
      emptyTitle="Nothing completed yet"
    >
      <TaskPreviewList tasks={tasks} dateField="completed" />
    </DashboardWidget>
  );
}
