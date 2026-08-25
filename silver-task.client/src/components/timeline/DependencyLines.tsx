import type { Task } from '@/types/task';
import type { TaskDependencyEdge } from '@/types/dependency';
import { daysBetween, displayRange } from '@/utils/timelineGrid';
import './DependencyLines.css';

interface DependencyLinesProps {
  /** Visible bar rows, in the same top-to-bottom order they're rendered — shared by Timeline and
   * Gantt (Gantt's chart engine *is* Timeline's, see GanttView's own doc comment), so this one
   * component draws lines for both instead of a second implementation. An edge where either end
   * isn't currently a visible row (filtered out, collapsed, or simply not scheduled) is just not
   * drawn — no attempt at cross-scroll or off-screen routing. */
  rows: Task[];
  edges: TaskDependencyEdge[];
  rangeStart: Date;
  pixelsPerDay: number;
  rowHeight: number;
  /** Row 0's vertical offset in px — Gantt has a project-header row above the task rows that
   * Timeline doesn't. */
  rowOffsetPx: number;
}

// A plain SVG overlay sharing the exact same coordinate space as the TimelineBar elements
// underneath it (same rangeStart/pixelsPerDay math, same row layout) — pointer-events: none so
// it never intercepts dragging/resizing/clicking a bar.
export function DependencyLines({ rows, edges, rangeStart, pixelsPerDay, rowHeight, rowOffsetPx }: DependencyLinesProps) {
  const taskById = new Map(rows.map((task) => [task.id, task]));
  const rowIndexById = new Map(rows.map((task, index) => [task.id, index]));

  const visibleEdges = edges.filter((edge) => rowIndexById.has(edge.taskId) && rowIndexById.has(edge.dependsOnTaskId));
  if (visibleEdges.length === 0) {
    return null;
  }

  function centerY(taskId: string): number {
    return rowOffsetPx + rowIndexById.get(taskId)! * rowHeight + rowHeight / 2;
  }

  function barRightX(task: Task): number {
    const { start, end } = displayRange(task);
    const left = daysBetween(rangeStart, start) * pixelsPerDay;
    const width = Math.max((daysBetween(start, end) + 1) * pixelsPerDay, pixelsPerDay);
    return left + width;
  }

  function barLeftX(task: Task): number {
    const { start } = displayRange(task);
    return daysBetween(rangeStart, start) * pixelsPerDay;
  }

  return (
    <svg className="dependency-lines" aria-hidden="true">
      <defs>
        <marker id="dependency-lines-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" className="dependency-lines__arrowhead" />
        </marker>
      </defs>
      {visibleEdges.map((edge) => {
        // Finish-to-Start: the line runs from the prerequisite's bar (end) to the dependent's
        // bar (start) — the only DependencyType this app currently interprets (see
        // Common/DependencyTypes.cs).
        const prerequisite = taskById.get(edge.dependsOnTaskId)!;
        const dependent = taskById.get(edge.taskId)!;
        const startX = barRightX(prerequisite);
        const startY = centerY(prerequisite.id);
        const endX = barLeftX(dependent);
        const endY = centerY(dependent.id);
        const midX = (startX + endX) / 2;
        const path = `M ${startX} ${startY} C ${midX} ${startY}, ${midX} ${endY}, ${endX} ${endY}`;

        return (
          <path
            key={`${edge.dependsOnTaskId}-${edge.taskId}`}
            d={path}
            className="dependency-lines__path"
            markerEnd="url(#dependency-lines-arrow)"
          />
        );
      })}
    </svg>
  );
}
