import { useEffect, useMemo, useRef, useState } from 'react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { useProjectDependencyEdges } from '@/hooks/useTaskDependencies';
import { addDays, toDateOnly } from '@/utils/calendarGrid';
import {
  PIXELS_PER_DAY,
  buildTimelineMonthBands,
  buildTimelineTicks,
  computeDateRange,
  daysBetween,
  displayRange,
  tasksWithDates,
  tasksWithoutDates,
  type TimelineScale,
} from '@/utils/timelineGrid';
import { TimelineBar } from './TimelineBar';
import { TimelineRuler } from './TimelineRuler';
import { TimelineScaleToolbar } from './TimelineScaleToolbar';
import { UnscheduledTray } from './UnscheduledTray';
import { DependencyLines } from './DependencyLines';
import './TimelineView.css';

const ROW_HEIGHT = 40;
const BAR_HEIGHT = 26;

interface TimelineViewProps {
  projectId: string;
  /** Same filtered/sorted task list every other project view renders — row order follows
   * whatever the toolbar's current sort is, same as Kanban preserving it within columns. */
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
}

// Drag-to-move/resize reuses useUpdateTask + the taskFieldChange.dateRange helper — same
// optimistic update, same rollback-on-failure, same PUT /api/tasks/{id} as every other date
// edit in the app, just driven by a pointer gesture instead of the date-picker inputs.
export function TimelineView({ projectId, tasks, onOpenDetail }: TimelineViewProps) {
  const updateTask = useUpdateTask(projectId);
  const { data: dependencyEdges } = useProjectDependencyEdges(projectId);
  const [scale, setScale] = useState<TimelineScale>('week');
  const scrollRef = useRef<HTMLDivElement>(null);

  const scheduled = useMemo(() => tasksWithDates(tasks), [tasks]);
  const unscheduled = useMemo(() => tasksWithoutDates(tasks), [tasks]);
  const errorTaskId = updateTask.isError ? (updateTask.variables?.task.id ?? null) : null;

  const pixelsPerDay = PIXELS_PER_DAY[scale];

  const { rangeStart, rangeEnd } = useMemo(() => computeDateRange(scheduled), [scheduled]);
  const totalDays = daysBetween(rangeStart, rangeEnd) + 1;
  const totalWidth = totalDays * pixelsPerDay;

  // Midnight-normalized so the boundary comparison below can't be thrown off by the current
  // time-of-day (rangeStart/rangeEnd, like every other date here, are always local midnight).
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const todayInRange = today >= rangeStart && today <= rangeEnd;
  const todayLeft = todayInRange ? daysBetween(rangeStart, today) * pixelsPerDay : null;

  const ticks = useMemo(() => buildTimelineTicks(scale, rangeStart, rangeEnd), [scale, rangeStart, rangeEnd]);
  const monthBands = useMemo(
    () => (scale === 'month' ? [] : buildTimelineMonthBands(rangeStart, rangeEnd, pixelsPerDay)),
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
      <TimelineScaleToolbar scale={scale} onScaleChange={setScale} onToday={scrollToToday} />

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
            <TimelineRuler ticks={ticks} monthBands={monthBands} />

            <div className="timeline-view__rows" style={{ height: scheduled.length * ROW_HEIGHT }}>
              {todayLeft !== null && <div className="timeline-view__today-line" style={{ left: todayLeft }} />}
              <DependencyLines
                rows={scheduled}
                edges={dependencyEdges ?? []}
                rangeStart={rangeStart}
                pixelsPerDay={pixelsPerDay}
                rowHeight={ROW_HEIGHT}
                rowOffsetPx={0}
              />
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
