import type { TaskPriority } from '@/types/task';
import './Badges.css';

interface PriorityBadgeProps {
  priority: TaskPriority;
}

export function PriorityBadge({ priority }: PriorityBadgeProps) {
  return <span className={`priority-badge priority-badge--${priority.toLowerCase()}`}>{priority}</span>;
}
