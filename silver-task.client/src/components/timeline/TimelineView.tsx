import { useEffect, useMemo, useRef, useState } from 'react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { addDays, startOfWeek, toDateOnly } from '@/utils/calendarGrid';
import { daysBetween, displayRange, tasksWithDates, tasksWithoutDates } from '@/utils/timelineGrid';
import { TimelineBar } from './TimelineBar';
import { UnscheduledTray } from './UnscheduledTray';
import './TimelineView.css';

type TimelineScale = 'day' | 'week' | 'month';

const PIXELS_PER_DAY: Record<TimelineScale, number> = { day: 36, week: 12, month: 4 };
const ROW_HEIGHT = 40;
const BAR_HEIGHT = 26;

interface TimelineViewProps {
  projectId: string;
  /** Same filtered/sorted task list every other project view renders — row order follows
   * whatever the toolbar's current sort is, same as Kanban preserving it within columns. */
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
}

// Drag-to-move/resize reuses useUpdateTask + the new taskFieldChange.dateRange helper — same
// optimistic update, same rollback-on-failure, same PUT /api/tasks/{id} as every other date
// edit in the app, just driven by a pointer gesture instead of the date-picker inputs.
export function TimelineView({ projectId, tasks, onOpenDetail }: TimelineViewProps) {
  const updateTask = useUpdateTask(projectId);
  const [scale, setScale] = useState<TimelineScale>('week');
  const scrollRef = useRef<HTMLDivElement>(null);

  const scheduled = useMemo(() => tasksWithDates(tasks), [tasks]);
  const unscheduled = useMemo(() => tasksWithoutDates(tasks), [tasks]);
  const errorTaskId = updateTask.isError ? (updateTask.variables?.task.id ?? null) : null;

  const pixelsPerDay = PIXELS_PER_DAY[scale];

  const { rangeStart, rangeEnd } = useMemo(() => computeRange(scheduled), [scheduled]);
  const totalDays = daysBetween(rangeStart, rangeEnd) + 1;
  const totalWidth = totalDays * pixelsPerDay;

  // Midnight-normalized so the boundary comparison below can't be thrown off by the current
  // time-of-day (rangeStart/rangeEnd, like every other date here, are always local midnight).
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const todayInRange = today >= rangeStart && today <= rangeEnd;
  const todayLeft = todayInRange ? daysBetween(rangeStart, today) * pixelsPerDay : null;

  const ticks = useMemo(() => buildTicks(scale, rangeStart, rangeEnd), [scale, rangeStart, rangeEnd]);
  const monthBands = useMemo(
    () => (scale === 'month' ? [] : buildMonthBands(rangeStart, rangeEnd, pixelsPerDay)),
    [scale, rangeStart, rangeEnd, pixelsPerDay],
  );

  // Center the initial view on today (or the range midpoint, if today is outside it) once per
  // scale change — a fresh chart shouldn't force the user to scroll to find "now".
  useEffect(() => {
    if (!scrollRef.current) {
      return;
    }
    const target = todayLeft ?? totalWidth / 2;
    scrollRef.current.scrollLeft = Math.max(0, target - scrollRef.current.clientWidth / 2);
    // Re-center whenever the scale (and therefore pixelsPerDay/totalWidth) changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scale]);

  function scrollToToday() {
    if (!scrollRef.current) {
      return;
    }
    const target = todayLeft ?? totalWidth / 2;
    scrollRef.current.scrollTo({ left: Math.max(0, target - scrollRef.current.clientWidth / 2), behavior: 'smooth' });
  }

  function handleBarDragEnd(task: Task, mode: 'move' | 'resize-left' | 'resize-right', deltaDays: number) {
    const { start, end } = displayRange(task);

    if (mode === 'move') {
      const newStart = task.startDate ? toDateOnly(addDays(start, deltaDays)) : null;
      const newDue = task.dueDate ? toDateOnly(addDays(end, deltaDays)) : null;
      updateTask.mutate({ task, change: taskFieldChange.dateRange(newStart, newDue) });
      return;
    }

    if (mode === 'resize-left') {
      let newStart = addDays(start, deltaDays);
      if (task.dueDate && newStart > end) {
        newStart = end;
      }
      updateTask.mutate({ task, change: taskFieldChange.dateRange(toDateOnly(newStart), task.dueDate) });
      return;
    }

    let newEnd = addDays(end, deltaDays);
    if (task.startDate && newEnd < start) {
      newEnd = start;
    }
    updateTask.mutate({ task, change: taskFieldChange.dateRange(task.startDate, toDateOnly(newEnd)) });
  }

  return (
    <div className="timeline-view">
      <div className="timeline-view__toolbar">
        <button type="button" className="timeline-view__today-btn" onClick={scrollToToday}>
          Today
        </button>
        <div className="timeline-view__scale-switch" role="tablist">
          {(['day', 'week', 'month'] as const).map((option) => (
            <button
              key={option}
              type="button"
              role="tab"
              aria-selected={scale === option}
              className={`timeline-view__scale-item${scale === option ? ' timeline-view__scale-item--active' : ''}`}
              onClick={() => setScale(option)}
            >
              {option[0].toUpperCase() + option.slice(1)}
            </button>
          ))}
        </div>
      </div>

      <div className="timeline-view__body">
        <div className="timeline-view__labels">
          <div className="timeline-view__label-spacer" />
          {scheduled.map((task) => (
            <div className="timeline-row-label" key={task.id} style={{ height: ROW_HEIGHT }} title={task.title}>
              {task.title}
            </div>
          ))}
        </div>

        <div className="timeline-view__chart-scroll" ref={scrollRef}>
          <div className="timeline-view__chart" style={{ width: totalWidth }}>
            <div className="timeline-view__ruler">
              {monthBands.map((band) => (
                <div key={band.key} className="timeline-view__month-band" style={{ left: band.left, width: band.width }}>
                  <span>{band.label}</span>
                </div>
              ))}
              <div className="timeline-view__ticks">
                {ticks.map((tick) => (
                  <div key={tick.key} className="timeline-view__tick" style={{ left: tick.left }}>
                    <span>{tick.label}</span>
                  </div>
                ))}
              </div>
            </div>

            <div className="timeline-view__rows" style={{ height: scheduled.length * ROW_HEIGHT }}>
              {todayLeft !== null && <div className="timeline-view__today-line" style={{ left: todayLeft }} />}
              {scheduled.map((task, rowIndex) => {
                const { start, end } = displayRange(task);
                const left = daysBetween(rangeStart, start) * pixelsPerDay;
                const width = (daysBetween(start, end) + 1) * pixelsPerDay;
                return (
                  <TimelineBar
                    key={task.id}
                    task={task}
                    left={left}
                    width={Math.max(width, pixelsPerDay)}
                    top={rowIndex * ROW_HEIGHT + (ROW_HEIGHT - BAR_HEIGHT) / 2}
                    height={BAR_HEIGHT}
                    pixelsPerDay={pixelsPerDay}
                    hasError={errorTaskId === task.id}
                    onOpenDetail={() => onOpenDetail(task.id)}
                    onDragEnd={(mode, deltaDays) => handleBarDragEnd(task, mode, deltaDays)}
                  />
                );
              })}
            </div>
          </div>
        </div>
      </div>

      <UnscheduledTray tasks={unscheduled} onOpenDetail={onOpenDetail} />
    </div>
  );
}

function computeRange(scheduled: Task[]): { rangeStart: Date; rangeEnd: Date } {
  if (scheduled.length === 0) {
    const today = new Date();
    return { rangeStart: addDays(today, -7), rangeEnd: addDays(today, 21) };
  }

  let minDate: Date | null = null;
  let maxDate: Date | null = null;
  for (const task of scheduled) {
    const { start, end } = displayRange(task);
    if (!minDate || start < minDate) {
      minDate = start;
    }
    if (!maxDate || end > maxDate) {
      maxDate = end;
    }
  }

  return { rangeStart: addDays(minDate!, -3), rangeEnd: addDays(maxDate!, 3) };
}

interface Tick {
  key: string;
  left: number;
  label: string;
}

function buildTicks(scale: TimelineScale, rangeStart: Date, rangeEnd: Date): Tick[] {
  if (scale === 'day') {
    const totalDays = daysBetween(rangeStart, rangeEnd) + 1;
    return Array.from({ length: totalDays }, (_, i) => {
      const date = addDays(rangeStart, i);
      return {
        key: toDateOnly(date),
        left: i * PIXELS_PER_DAY.day,
        label: String(date.getDate()),
      };
    });
  }

  if (scale === 'week') {
    const ticks: Tick[] = [];
    for (let date = startOfWeek(rangeStart); date <= rangeEnd; date = addDays(date, 7)) {
      const left = daysBetween(rangeStart, date) * PIXELS_PER_DAY.week;
      // startOfWeek(rangeStart) can land a few days before rangeStart itself — skip the
      // partial tick rather than rendering it at a negative (invisible/clipped) position.
      if (left < 0) {
        continue;
      }
      ticks.push({
        key: toDateOnly(date),
        left,
        label: date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      });
    }
    return ticks;
  }

  const ticks: Tick[] = [];
  for (let date = new Date(rangeStart.getFullYear(), rangeStart.getMonth(), 1); date <= rangeEnd; date = new Date(date.getFullYear(), date.getMonth() + 1, 1)) {
    ticks.push({
      key: toDateOnly(date),
      left: daysBetween(rangeStart, date) * PIXELS_PER_DAY.month,
      label: date.toLocaleDateString(undefined, { month: 'short', year: 'numeric' }),
    });
  }
  return ticks;
}

interface MonthBand {
  key: string;
  left: number;
  width: number;
  label: string;
}

/** Only used at Day/Week scale, where a single tick can't convey "which month" on its own —
 * Month scale's ticks already are months, so no separate band row is needed there. */
function buildMonthBands(rangeStart: Date, rangeEnd: Date, pixelsPerDay: number): MonthBand[] {
  const bands: MonthBand[] = [];
  let cursor = new Date(rangeStart.getFullYear(), rangeStart.getMonth(), 1);

  while (cursor <= rangeEnd) {
    const monthStart = cursor < rangeStart ? rangeStart : cursor;
    const nextMonth = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1);
    const monthEnd = addDays(nextMonth, -1) > rangeEnd ? rangeEnd : addDays(nextMonth, -1);

    bands.push({
      key: toDateOnly(cursor),
      left: daysBetween(rangeStart, monthStart) * pixelsPerDay,
      width: (daysBetween(monthStart, monthEnd) + 1) * pixelsPerDay,
      label: cursor.toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
    });

    cursor = nextMonth;
  }

  return bands;
}
