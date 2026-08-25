import { useEffect, useMemo, useRef, useState } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { useProjectDependencyEdges } from '@/hooks/useTaskDependencies';
import { addDays, toDateOnly } from '@/utils/calendarGrid';
import { buildGanttRows } from '@/utils/taskHierarchy';
import {
  PIXELS_PER_DAY,
  buildTimelineMonthBands,
  buildTimelineTicks,
  computeDateRange,
  daysBetween,
  displayRange,
  tasksWithDates,
  type TimelineScale,
} from '@/utils/timelineGrid';
import { TimelineBar } from '@/components/timeline/TimelineBar';
import { TimelineRuler } from '@/components/timeline/TimelineRuler';
import { TimelineScaleToolbar } from '@/components/timeline/TimelineScaleToolbar';
import { UnscheduledTray } from '@/components/timeline/UnscheduledTray';
import { DependencyLines } from '@/components/timeline/DependencyLines';
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
 * Dependencies (Phase 29): DependencyLines — the same component Timeline uses — draws a
 * Finish-to-Start connector between two visible bars' positions in `.gantt-view__rows`, offset
 * by the project-header row Timeline doesn't have (see rowOffsetPx below).
 */
export function GanttView({ projectId, projectName, tasks, onOpenDetail }: GanttViewProps) {
  const updateTask = useUpdateTask(projectId);
  const { data: dependencyEdges } = useProjectDependencyEdges(projectId);
  const [scale, setScale] = useState<TimelineScale>('week');
  const [isExpanded, setIsExpanded] = useState(true);
  const [collapsedTaskIds, setCollapsedTaskIds] = useState<Set<string>>(new Set());
  const scrollRef = useRef<HTMLDivElement>(null);

  const scheduled = useMemo(() => tasksWithDates(tasks), [tasks]);
  // Hierarchy-aware row list — includes parent tasks that have no dates of their own but do have
  // at least one dated descendant, with a computed (display-only, never persisted) date range.
  const ganttRows = useMemo(() => buildGanttRows(tasks, collapsedTaskIds), [tasks, collapsedTaskIds]);
  // The unfiltered/fully-expanded id set determines what's "renderable" on the chart at all —
  // used to keep calculated-only parents (and their dated descendants, when the parent row is
  // currently collapsed) out of the Unscheduled tray, independent of the current collapse state.
  const renderableIds = useMemo(
    () => new Set(buildGanttRows(tasks, new Set()).map((row) => row.task.id)),
    [tasks],
  );
  const unscheduled = useMemo(
    () => tasks.filter((task) => task.startDate === null && task.dueDate === null && !renderableIds.has(task.id)),
    [tasks, renderableIds],
  );
  const errorTaskId = updateTask.isError ? (updateTask.variables?.task.id ?? null) : null;
  const visibleRows = isExpanded ? ganttRows : [];

  function toggleTaskCollapse(taskId: string) {
    setCollapsedTaskIds((prev) => {
      const next = new Set(prev);
      if (next.has(taskId)) {
        next.delete(taskId);
      } else {
        next.add(taskId);
      }
      return next;
    });
  }

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
          {visibleRows.map((row) => (
            <div
              className={`timeline-row-label gantt-view__task-row-label${row.task.subtaskCount > 0 ? ' timeline-row-label--parent' : ''}`}
              key={row.task.id}
              style={{ height: ROW_HEIGHT, paddingLeft: 10 + row.depth * 16 }}
              title={row.task.title}
            >
              {row.hasChildren ? (
                <button
                  type="button"
                  className="gantt-view__row-expand-toggle"
                  aria-label={collapsedTaskIds.has(row.task.id) ? 'Expand subtasks' : 'Collapse subtasks'}
                  onClick={(event) => {
                    event.stopPropagation();
                    toggleTaskCollapse(row.task.id);
                  }}
                >
                  {collapsedTaskIds.has(row.task.id) ? <ChevronRight size={12} /> : <ChevronDown size={12} />}
                </button>
              ) : (
                <span className="gantt-view__row-expand-spacer" />
              )}
              <span className="gantt-view__row-label-text">{row.task.title}</span>
            </div>
          ))}
        </div>

        <div className="timeline-view__chart-scroll" ref={scrollRef}>
          <div className="timeline-view__chart" style={{ width: totalWidth }}>
            <TimelineRuler ticks={ticks} monthBands={monthBands} />

            <div className="timeline-view__rows" style={{ height: (1 + visibleRows.length) * ROW_HEIGHT }}>
              {todayLeft !== null && <div className="timeline-view__today-line" style={{ left: todayLeft }} />}

              <DependencyLines
                rows={visibleRows.map((row) => row.task)}
                edges={dependencyEdges ?? []}
                rangeStart={rangeStart}
                pixelsPerDay={pixelsPerDay}
                rowHeight={ROW_HEIGHT}
                rowOffsetPx={ROW_HEIGHT}
              />

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

              {visibleRows.map((row, rowIndex) => {
                const { task, range, isCalculated } = row;
                const left = daysBetween(rangeStart, range.start) * pixelsPerDay;
                const width = Math.max((daysBetween(range.start, range.end) + 1) * pixelsPerDay, pixelsPerDay);
                const rowTop = (rowIndex + 1) * ROW_HEIGHT;

                // Calculated (parent-with-no-dates-of-its-own) rows render as a non-interactive
                // summary bar — never draggable, since there are no real dates on the task to
                // move, and this range must never be written back to the task's own fields.
                if (isCalculated) {
                  return (
                    <div
                      key={task.id}
                      className="gantt-view__calculated-bar"
                      style={{ left, width, top: rowTop + (ROW_HEIGHT - SUMMARY_BAR_HEIGHT) / 2, height: SUMMARY_BAR_HEIGHT }}
                      title={`${task.title} — calculated from subtask dates, not a saved date`}
                      role="button"
                      tabIndex={0}
                      onClick={() => onOpenDetail(task.id)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          onOpenDetail(task.id);
                        }
                      }}
                    />
                  );
                }

                return (
                  <TimelineBar
                    key={task.id}
                    task={task}
                    left={left}
                    width={width}
                    top={rowTop + (ROW_HEIGHT - BAR_HEIGHT) / 2}
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
