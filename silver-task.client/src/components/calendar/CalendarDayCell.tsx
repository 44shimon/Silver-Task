import type { DragEvent } from 'react';
import type { Task } from '@/types/task';
import { CalendarTaskChip } from './CalendarTaskChip';
import './CalendarDayCell.css';

interface CalendarDayCellProps {
  date: Date;
  dateOnly: string;
  tasks: Task[];
  isCurrentMonth: boolean;
  isToday: boolean;
  /** Undefined shows every task (Week view has more room); Month view caps this and shows
   * "+N more" so a busy day doesn't blow out the row height of the whole grid. */
  maxVisible?: number;
  isDragOver: boolean;
  draggingTaskId: string | null;
  errorTaskId: string | null;
  onCardDragStart: (taskId: string) => void;
  onCardDragEnd: () => void;
  onDragEnter: () => void;
  onDragLeave: (event: DragEvent<HTMLDivElement>) => void;
  onDrop: (taskId: string) => void;
  onOpenDetail: (taskId: string) => void;
  onSelectDay: (date: Date) => void;
}

export function CalendarDayCell({
  date,
  tasks,
  isCurrentMonth,
  isToday,
  maxVisible,
  isDragOver,
  draggingTaskId,
  errorTaskId,
  onCardDragStart,
  onCardDragEnd,
  onDragEnter,
  onDragLeave,
  onDrop,
  onOpenDetail,
  onSelectDay,
}: CalendarDayCellProps) {
  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    const taskId = event.dataTransfer.getData('text/plain');
    if (taskId) {
      onDrop(taskId);
    }
  }

  const visibleTasks = maxVisible ? tasks.slice(0, maxVisible) : tasks;
  const hiddenCount = tasks.length - visibleTasks.length;

  return (
    <div
      className={`calendar-day-cell${isCurrentMonth ? '' : ' calendar-day-cell--outside'}${isDragOver ? ' calendar-day-cell--drag-over' : ''}`}
      onDragOver={(e) => e.preventDefault()}
      onDragEnter={onDragEnter}
      onDragLeave={onDragLeave}
      onDrop={handleDrop}
    >
      <button type="button" className={`calendar-day-cell__date${isToday ? ' calendar-day-cell__date--today' : ''}`} onClick={() => onSelectDay(date)}>
        {date.getDate()}
      </button>

      <div className="calendar-day-cell__tasks">
        {visibleTasks.map((task) => (
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
        {hiddenCount > 0 && (
          <button type="button" className="calendar-day-cell__more" onClick={() => onSelectDay(date)}>
            +{hiddenCount} more
          </button>
        )}
      </div>
    </div>
  );
}
