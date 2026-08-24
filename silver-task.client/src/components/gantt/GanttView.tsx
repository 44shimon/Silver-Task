import { useEffect, useMemo, useRef, useState } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
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
import { TimelineBar } from '@/components/timeline/TimelineBar';
import { TimelineRuler } from '@/components/timeline/TimelineRuler';
import { TimelineScaleToolbar } from '@/components/timeline/TimelineScaleToolbar';
import { UnscheduledTray } from '@/components/timeline/UnscheduledTray';
import '@/components/timeline/TimelineView.css';
import './GanttView.css';

const ROW_HEIGHT = 40;
const BAR_HEIGHT = 26;
const SUMMARY_BAR_HEIGHT = 10;

interface GanttViewProps {
  projectId: string;
  projectName: string;
  /** Same filtered/sorted task list every other project view renders. */
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
}

/**
 * The Timeline view's chart engine (ruler, ticks, month bands, zoom, drag-move/resize) plus a
 * project/task hierarchy layer on top — not a second implementation of the date-grid math, per
 * the "don't duplicate" principle. The only real difference from Timeline is that task rows sit
 * under a collapsible project header (with a summary bar spanning its own tasks' date range)
 * instead of a flat list. A future cross-project Gantt would repeat this same group-header
 * pattern once per project rather than needing new chart logic.
 *
 * Dependencies (task A blocks task B) are intentionally not implemented — the current schema
 * has no dependency table/columns (TaskItem has no BlockedByTaskId or similar), and the spec is
 * explicit not to build a "complicated dependency system" the architecture doesn't already
 * support. The extension point, if that's added later, is here: a dependency line would be
 * drawn between two TimelineBar positions in `.gantt-view__rows`, keyed off whatever new field
 * a future migration adds to TaskItem.
 */
export function GanttView({ projectId, projectName, tasks, onOpenDetail }: GanttViewProps) {
  const updateTask = useUpdateTask(projectId);
  const [scale, setScale] = useState<TimelineScale>('week');
  const [isExpanded, setIsExpanded] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);

  const scheduled = useMemo(() => tasksWithDates(tasks), [tasks]);
  const unscheduled = useMemo(() => tasksWithoutDates(tasks), [tasks]);
  const errorTaskId = updateTask.isError ? (updateTask.variables?.task.id ?? null) : null;
  const visibleRows = isExpanded ? scheduled : [];

  const pixelsPerDay = PIXELS_PER_DAY[scale];

  const { rangeStart, rangeEnd } = useMemo(() => computeDateRange(scheduled), [scheduled]);
  const totalDays = daysBetween(rangeStart, rangeEnd) + 1;
  const totalWidth = totalDays * pixelsPerDay;

  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const todayInRange = today >= rangeStart && today <= rangeEnd;
  const todayLeft = todayInRange ? daysBetween(rangeStart, today) * pixelsPerDay : null;

  const ticks = useMemo(() => buildTimelineTicks(scale, rangeStart, rangeEnd), [scale, rangeStart, rangeEnd]);
  const monthBands = useMemo(
    () => (scale === 'month' ? [] : buildTimelineMonthBands(rangeStart, rangeEnd, pixelsPerDay)),
    [scale, rangeStart, rangeEnd, pixelsPerDay],
  );

  // The project group's own overview bar — earliest start to latest due across its tasks.
  // Static/non-interactive (it doesn't correspond to a single editable field), standard
  // convention for a Gantt group-header row.
  const summaryRange = useMemo(() => {
    if (scheduled.length === 0) {
      return null;
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
    return { start: minDate!, end: maxDate! };
  }, [scheduled]);

  useEffect(() => {
    if (!scrollRef.current) {
      return;
    }
    const target = todayLeft ?? totalWidth / 2;
    scrollRef.current.scrollLeft = Math.max(0, target - scrollRef.current.clientWidth / 2);
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
          <button type="button" className="gantt-view__project-row" style={{ height: ROW_HEIGHT }} onClick={() => setIsExpanded((v) => !v)}>
            {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
            <span>{projectName}</span>
          </button>
          {visibleRows.map((task) => (
            <div className="timeline-row-label gantt-view__task-row-label" key={task.id} style={{ height: ROW_HEIGHT }} title={task.title}>
              {task.title}
            </div>
          ))}
        </div>

        <div className="timeline-view__chart-scroll" ref={scrollRef}>
          <div className="timeline-view__chart" style={{ width: totalWidth }}>
            <TimelineRuler ticks={ticks} monthBands={monthBands} />

            <div className="timeline-view__rows" style={{ height: (1 + visibleRows.length) * ROW_HEIGHT }}>
              {todayLeft !== null && <div className="timeline-view__today-line" style={{ left: todayLeft }} />}

              {summaryRange && (
                <div
                  className="gantt-view__summary-bar"
                  style={{
                    left: daysBetween(rangeStart, summaryRange.start) * pixelsPerDay,
                    width: Math.max((daysBetween(summaryRange.start, summaryRange.end) + 1) * pixelsPerDay, pixelsPerDay),
                    top: (ROW_HEIGHT - SUMMARY_BAR_HEIGHT) / 2,
                    height: SUMMARY_BAR_HEIGHT,
                  }}
                  title={`${projectName}: ${scheduled.length} scheduled task${scheduled.length === 1 ? '' : 's'}`}
                />
              )}

              {visibleRows.map((task, rowIndex) => {
                const { start, end } = displayRange(task);
                const left = daysBetween(rangeStart, start) * pixelsPerDay;
                const width = (daysBetween(start, end) + 1) * pixelsPerDay;
                return (
                  <TimelineBar
                    key={task.id}
                    task={task}
                    left={left}
                    width={Math.max(width, pixelsPerDay)}
                    top={(rowIndex + 1) * ROW_HEIGHT + (ROW_HEIGHT - BAR_HEIGHT) / 2}
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
