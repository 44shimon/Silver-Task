import type { DragEvent } from 'react';
import type { Task } from '@/types/task';
import { toDateOnly } from '@/utils/calendarGrid';
import { CalendarTaskChip } from './CalendarTaskChip';
import './DayView.css';

interface DayViewProps {
  anchor: Date;
  tasksByDate: Map<string, Task[]>;
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

export function DayView({
  anchor,
  tasksByDate,
  draggingTaskId,
  errorTaskId,
  isDragOver,
  onCardDragStart,
  onCardDragEnd,
  onDragEnter,
  onDragLeave,
  onDrop,
  onOpenDetail,
}: DayViewProps) {
  const dateOnly = toDateOnly(anchor);
  const tasks = tasksByDate.get(dateOnly) ?? [];

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    const taskId = event.dataTransfer.getData('text/plain');
    if (taskId) {
      onDrop(taskId);
    }
  }

  return (
    <div
      className={`day-view${isDragOver ? ' day-view--drag-over' : ''}`}
      onDragOver={(e) => e.preventDefault()}
      onDragEnter={onDragEnter}
      onDragLeave={onDragLeave}
      onDrop={handleDrop}
    >
      {tasks.length === 0 && <p className="day-view__empty">No tasks due this day. Drag one here to schedule it.</p>}
      {tasks.map((task) => (
        <CalendarTaskChip
          key={task.id}
          task={task}
          variant="expanded"
          isDragging={draggingTaskId === task.id}
          hasError={errorTaskId === task.id}
          onDragStart={() => onCardDragStart(task.id)}
          onDragEnd={onCardDragEnd}
          onOpenDetail={() => onOpenDetail(task.id)}
        />
      ))}
    </div>
  );
}
