import type { DragEvent } from 'react';
import type { Task } from '@/types/task';
import { CalendarTaskChip } from './CalendarTaskChip';
import './NoDueDateTray.css';

interface NoDueDateTrayProps {
  tasks: Task[];
  draggingTaskId: string | null;
  errorTaskId: string | null;
  isDragOver: boolean;
  onCardDragStart: (taskId: string) => void;
  onCardDragEnd: () => void;
  onDragEnter: () => void;
  onDragLeave: (event: DragEvent<HTMLDivElement>) => void;
  onDrop: (taskId: string) => void;
  onOpenDetail: (taskId: string) => void;
}

// Tasks without a due date must not disappear from the Calendar view, per spec — this tray is
// always visible (not hidden behind a filter) and doubles as a drop target: dragging a
// scheduled task chip in here clears its due date, same taskFieldChange.dueDate(null) path
// EditableDateCell already uses when its date input is cleared.
export function NoDueDateTray({
  tasks,
  draggingTaskId,
  errorTaskId,
  isDragOver,
  onCardDragStart,
  onCardDragEnd,
  onDragEnter,
  onDragLeave,
  onDrop,
  onOpenDetail,
}: NoDueDateTrayProps) {
  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    const taskId = event.dataTransfer.getData('text/plain');
    if (taskId) {
      onDrop(taskId);
    }
  }

  return (
    <details className="no-due-date-tray" open={tasks.length > 0}>
      <summary>No Due Date ({tasks.length})</summary>
      <div
        className={`no-due-date-tray__body${isDragOver ? ' no-due-date-tray__body--drag-over' : ''}`}
        onDragOver={(e) => e.preventDefault()}
        onDragEnter={onDragEnter}
        onDragLeave={onDragLeave}
        onDrop={handleDrop}
      >
        {tasks.length === 0 && <p className="no-due-date-tray__empty">Every visible task has a due date.</p>}
        {tasks.map((task) => (
          <CalendarTaskChip
            key={task.id}
            task={task}
            variant="compact"
            isDragging={draggingTaskId === task.id}
            hasError={errorTaskId === task.id}
            onDragStart={() => onCardDragStart(task.id)}
            onDragEnd={onCardDragEnd}
            onOpenDetail={() => onOpenDetail(task.id)}
          />
        ))}
      </div>
    </details>
  );
}
