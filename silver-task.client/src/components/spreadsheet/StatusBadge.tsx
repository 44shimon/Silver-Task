import { STATUS_LABELS, type TaskStatus } from '@/types/task';
import './Badges.css';

interface StatusBadgeProps {
  status: TaskStatus;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  return <span className={`status-badge status-badge--${status.toLowerCase()}`}>{STATUS_LABELS[status]}</span>;
}
