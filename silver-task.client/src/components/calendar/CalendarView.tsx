import { useMemo, useState, type DragEvent } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { addDays, addMonths, buildWeekDays, groupTasksByDueDate, tasksWithoutDueDate, toDateOnly } from '@/utils/calendarGrid';
import { MonthView } from './MonthView';
import { WeekView } from './WeekView';
import { DayView } from './DayView';
import { NoDueDateTray } from './NoDueDateTray';
import './CalendarView.css';

type CalendarMode = 'month' | 'week' | 'day';

const NO_DATE_KEY = 'no-date';

interface CalendarViewProps {
  projectId: string;
  /** Same filtered/sorted task list every other project view renders — one dataset, multiple
   * visualizations, per the app's view architecture. */
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
  /** Phase 32 read-only mode — false blocks the drag-to-reschedule drop handler (the backend
   * independently rejects the write either way; this is a lighter-touch guard than fully
   * disabling each chip's `draggable` attribute — cards still visually drag, the drop itself is
   * just a no-op — a disclosed, bounded simplification given the number of chip components
   * across Month/Week/Day/NoDueDateTray this would otherwise touch). */
  canEdit: boolean;
}

// Drag-to-reschedule reuses useUpdateTask + taskFieldChange.dueDate exactly like
// EditableDateCell's Due Date editor in the Table view — same optimistic update, same
// rollback-on-failure, same PUT /api/tasks/{id}, just triggered by a drop instead of typing.
export function CalendarView({ projectId, tasks, onOpenDetail, canEdit }: CalendarViewProps) {
  const updateTask = useUpdateTask(projectId);
  const [mode, setMode] = useState<CalendarMode>('month');
  const [anchor, setAnchor] = useState(new Date());
  const [draggingTaskId, setDraggingTaskId] = useState<string | null>(null);
  const [dragOverKey, setDragOverKey] = useState<string | null>(null);

  const tasksByDate = useMemo(() => groupTasksByDueDate(tasks), [tasks]);
  const unscheduled = useMemo(() => tasksWithoutDueDate(tasks), [tasks]);
  const errorTaskId = updateTask.isError ? (updateTask.variables?.task.id ?? null) : null;

  const label = useMemo(() => formatPeriodLabel(mode, anchor), [mode, anchor]);

  function goPrev() {
    setAnchor((prev) => shiftAnchor(prev, mode, -1));
  }

  function goNext() {
    setAnchor((prev) => shiftAnchor(prev, mode, 1));
  }

  function goToday() {
    setAnchor(new Date());
  }

  function selectDay(date: Date) {
    setAnchor(date);
    setMode('day');
  }

  /** `dateOnly === null` means "clear the due date" (dropped on the No Due Date tray). */
  function handleDrop(taskId: string, dateOnly: string | null) {
    setDragOverKey(null);
    if (!canEdit) {
      return;
    }
    const task = tasks.find((t) => t.id === taskId);
    if (!task || task.dueDate === dateOnly) {
      return;
    }
    updateTask.mutate({ task, change: taskFieldChange.dueDate(dateOnly) });
  }

  function handleDragLeave(key: string, event: DragEvent<HTMLDivElement>) {
    if (!event.currentTarget.contains(event.relatedTarget as Node)) {
      setDragOverKey((current) => (current === key ? null : current));
    }
  }

  return (
    <div className="calendar-view">
      <div className="calendar-view__header">
        <div className="calendar-view__nav">
          <button type="button" className="icon-button" aria-label="Previous" onClick={goPrev}>
            <ChevronLeft size={16} />
          </button>
          <button type="button" className="calendar-view__today" onClick={goToday}>
            Today
          </button>
          <button type="button" className="icon-button" aria-label="Next" onClick={goNext}>
            <ChevronRight size={16} />
          </button>
          <span className="calendar-view__label">{label}</span>
        </div>

        <div className="calendar-view__mode-switch" role="tablist">
          {(['month', 'week', 'day'] as const).map((option) => (
            <button
              key={option}
              type="button"
              role="tab"
              aria-selected={mode === option}
              className={`calendar-view__mode-item${mode === option ? ' calendar-view__mode-item--active' : ''}`}
              onClick={() => setMode(option)}
            >
              {option[0].toUpperCase() + option.slice(1)}
            </button>
          ))}
        </div>
      </div>

      {mode === 'month' && (
        <MonthView
          anchor={anchor}
          tasksByDate={tasksByDate}
          draggingTaskId={draggingTaskId}
          errorTaskId={errorTaskId}
          dragOverKey={dragOverKey}
          onCardDragStart={setDraggingTaskId}
          onCardDragEnd={() => setDraggingTaskId(null)}
          onDragEnter={setDragOverKey}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onOpenDetail={onOpenDetail}
          onSelectDay={selectDay}
        />
      )}

      {mode === 'week' && (
        <WeekView
          anchor={anchor}
          tasksByDate={tasksByDate}
          draggingTaskId={draggingTaskId}
          errorTaskId={errorTaskId}
          dragOverKey={dragOverKey}
          onCardDragStart={setDraggingTaskId}
          onCardDragEnd={() => setDraggingTaskId(null)}
          onDragEnter={setDragOverKey}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onOpenDetail={onOpenDetail}
          onSelectDay={selectDay}
        />
      )}

      {mode === 'day' && (
        <DayView
          anchor={anchor}
          tasksByDate={tasksByDate}
          draggingTaskId={draggingTaskId}
          errorTaskId={errorTaskId}
          isDragOver={dragOverKey === toDateOnly(anchor)}
          onCardDragStart={setDraggingTaskId}
          onCardDragEnd={() => setDraggingTaskId(null)}
          onDragEnter={() => setDragOverKey(toDateOnly(anchor))}
          onDragLeave={(event) => handleDragLeave(toDateOnly(anchor), event)}
          onDrop={(taskId) => handleDrop(taskId, toDateOnly(anchor))}
          onOpenDetail={onOpenDetail}
        />
      )}

      <NoDueDateTray
        tasks={unscheduled}
        draggingTaskId={draggingTaskId}
        errorTaskId={errorTaskId}
        isDragOver={dragOverKey === NO_DATE_KEY}
        onCardDragStart={setDraggingTaskId}
        onCardDragEnd={() => setDraggingTaskId(null)}
        onDragEnter={() => setDragOverKey(NO_DATE_KEY)}
        onDragLeave={(event) => handleDragLeave(NO_DATE_KEY, event)}
        onDrop={(taskId) => handleDrop(taskId, null)}
        onOpenDetail={onOpenDetail}
      />
    </div>
  );
}

function shiftAnchor(date: Date, mode: CalendarMode, direction: 1 | -1): Date {
  if (mode === 'month') {
    return addMonths(date, direction);
  }
  if (mode === 'week') {
    return addDays(date, 7 * direction);
  }
  return addDays(date, direction);
}

function formatPeriodLabel(mode: CalendarMode, anchor: Date): string {
  if (mode === 'month') {
    return anchor.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
  }
  if (mode === 'day') {
    return anchor.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
  }

  const days = buildWeekDays(anchor);
  const start = days[0];
  const end = days[6];
  const sameMonth = start.getMonth() === end.getMonth();
  const startLabel = start.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  const endLabel = end.toLocaleDateString(
    undefined,
    sameMonth ? { day: 'numeric', year: 'numeric' } : { month: 'short', day: 'numeric', year: 'numeric' },
  );
  return `${startLabel} – ${endLabel}`;
}
