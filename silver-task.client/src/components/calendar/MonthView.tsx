import type { DragEvent } from 'react';
import type { Task } from '@/types/task';
import { buildMonthDays, isSameDay, toDateOnly } from '@/utils/calendarGrid';
import { CalendarDayCell } from './CalendarDayCell';
import './MonthView.css';

const WEEKDAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

interface MonthViewProps {
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

export function MonthView({
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
}: MonthViewProps) {
  const days = buildMonthDays(anchor);
  const today = new Date();

  return (
    <div className="month-view">
      <div className="month-view__weekdays">
        {WEEKDAY_LABELS.map((label) => (
          <div key={label} className="month-view__weekday">
            {label}
          </div>
        ))}
      </div>
      <div className="month-view__grid">
        {days.map((date) => {
          const dateOnly = toDateOnly(date);
          return (
            <CalendarDayCell
              key={dateOnly}
              date={date}
              dateOnly={dateOnly}
              tasks={tasksByDate.get(dateOnly) ?? []}
              isCurrentMonth={date.getMonth() === anchor.getMonth()}
              isToday={isSameDay(date, today)}
              maxVisible={3}
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
          );
        })}
      </div>
    </div>
  );
}
