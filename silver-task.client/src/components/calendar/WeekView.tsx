import type { DragEvent } from 'react';
import type { Task } from '@/types/task';
import { buildWeekDays, isSameDay, toDateOnly } from '@/utils/calendarGrid';
import { CalendarDayCell } from './CalendarDayCell';
import './WeekView.css';

interface WeekViewProps {
  anchor: Date;
  tasksByDate: Map<string, Task[]>;
  draggingTaskId: string | null;
  errorTaskId: string | null;
  dragOverKey: string | null;
  onCardDragStart: (taskId: string) => void;
  onCardDragEnd: () => void;
  onDragEnter: (dateOnly: string) => void;
  onDragLeave: (dateOnly: string, event: DragEvent<HTMLDivElement>) => void;
  onDrop: (taskId: string, dateOnly: string) => void;
  onOpenDetail: (taskId: string) => void;
  onSelectDay: (date: Date) => void;
}

export function WeekView({
  anchor,
  tasksByDate,
  draggingTaskId,
  errorTaskId,
  dragOverKey,
  onCardDragStart,
  onCardDragEnd,
  onDragEnter,
  onDragLeave,
  onDrop,
  onOpenDetail,
  onSelectDay,
}: WeekViewProps) {
  const days = buildWeekDays(anchor);
  const today = new Date();

  return (
    <div className="week-view">
      {days.map((date) => {
        const dateOnly = toDateOnly(date);
        return (
          <div className="week-view__day" key={dateOnly}>
            <div className="week-view__day-label">{date.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' })}</div>
            <CalendarDayCell
              date={date}
              dateOnly={dateOnly}
              tasks={tasksByDate.get(dateOnly) ?? []}
              isCurrentMonth
              isToday={isSameDay(date, today)}
              isDragOver={dragOverKey === dateOnly}
              draggingTaskId={draggingTaskId}
              errorTaskId={errorTaskId}
              onCardDragStart={onCardDragStart}
              onCardDragEnd={onCardDragEnd}
              onDragEnter={() => onDragEnter(dateOnly)}
              onDragLeave={(event) => onDragLeave(dateOnly, event)}
              onDrop={(taskId) => onDrop(taskId, dateOnly)}
              onOpenDetail={onOpenDetail}
              onSelectDay={onSelectDay}
            />
          </div>
        );
      })}
    </div>
  );
}
