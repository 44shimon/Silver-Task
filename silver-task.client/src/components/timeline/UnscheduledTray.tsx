import type { Task } from '@/types/task';
import { CalendarTaskChip } from '@/components/calendar/CalendarTaskChip';
import './UnscheduledTray.css';

interface UnscheduledTrayProps {
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
}

// Tasks with neither a Start Date nor a Due Date can't be placed as a bar — kept visible here
// instead of disappearing, same principle as the Calendar view's No Due Date tray (whose chip
// component this reuses directly rather than re-implementing task-chip rendering a third time).
export function UnscheduledTray({ tasks, onOpenDetail }: UnscheduledTrayProps) {
  return (
    <details className="unscheduled-tray" open={tasks.length > 0}>
      <summary>Unscheduled ({tasks.length})</summary>
      <div className="unscheduled-tray__body">
        {tasks.length === 0 && <p className="unscheduled-tray__empty">Every visible task has a start or due date.</p>}
        {tasks.map((task) => (
          <CalendarTaskChip
            key={task.id}
            task={task}
            variant="compact"
            isDragging={false}
            hasError={false}
            onDragStart={() => {}}
            onDragEnd={() => {}}
            onOpenDetail={() => onOpenDetail(task.id)}
          />
        ))}
      </div>
    </details>
  );
}
