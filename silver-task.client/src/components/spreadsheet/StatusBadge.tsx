import type { TaskStatus } from '@/types/task';
import './Badges.css';

const STATUS_LABELS: Record<TaskStatus, string> = {
  NotStarted: 'Not Started',
  InProgress: 'In Progress',
  Waiting: 'Waiting',
  Blocked: 'Blocked',
  Complete: 'Complete',
  Cancelled: 'Cancelled',
};

interface StatusBadgeProps {
  status: TaskStatus;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  return <span className={`status-badge status-badge--${status.toLowerCase()}`}>{STATUS_LABELS[status]}</span>;
}
