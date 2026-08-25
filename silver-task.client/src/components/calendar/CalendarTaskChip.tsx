import type { DragEvent, KeyboardEvent } from 'react';
import { Link2 } from 'lucide-react';
import { STATUS_LABELS, type Task } from '@/types/task';
import { StatusBadge } from '@/components/spreadsheet/StatusBadge';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import { initials } from '@/utils/initials';
import './CalendarTaskChip.css';

interface CalendarTaskChipProps {
  task: Task;
  /** "compact" for month/week grid cells (small chip, hover for the rest); "expanded" for the
   * Day view agenda, which has room to show Status/Priority/Assignee directly. */
  variant: 'compact' | 'expanded';
  isDragging: boolean;
  hasError: boolean;
  onDragStart: () => void;
  onDragEnd: () => void;
  onOpenDetail: () => void;
}

// Shared by Month/Week/Day so drag/click/error behavior is defined exactly once, not per view.
export function CalendarTaskChip({ task, variant, isDragging, hasError, onDragStart, onDragEnd, onOpenDetail }: CalendarTaskChipProps) {
  function handleDragStart(event: DragEvent<HTMLDivElement>) {
    event.dataTransfer.setData('text/plain', task.id);
    event.dataTransfer.effectAllowed = 'move';
    onDragStart();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onOpenDetail();
    }
  }

  // Compact chips are small (see below), so the rest of the required "Display" fields — Status,
  // Assigned user — surface as a native hover tooltip instead of taking up cell space. The
  // expanded (Day) variant shows them directly since there's room for that there.
  const blockedSuffix = task.blockedByCount > 0 ? `, blocked by ${task.blockedByCount}` : '';
  const tooltip = hasError
    ? 'Could not save the new due date — try dragging again'
    : variant === 'compact'
      ? `${task.title} — ${STATUS_LABELS[task.status]}, ${task.priority}${task.assignedTo ? `, assigned to ${task.assignedTo.name}` : ''}${blockedSuffix}`
      : undefined;

  const commonProps = {
    draggable: true,
    onDragStart: handleDragStart,
    onDragEnd,
    onClick: onOpenDetail,
    role: 'button' as const,
    tabIndex: 0,
    onKeyDown: handleKeyDown,
    title: tooltip,
  };

  if (variant === 'expanded') {
    return (
      <div
        {...commonProps}
        className={`calendar-chip calendar-chip--expanded${isDragging ? ' calendar-chip--dragging' : ''}${hasError ? ' calendar-chip--error' : ''}`}
      >
        <span className="calendar-chip__title">{task.title}</span>
        <div className="calendar-chip__expanded-meta">
          <StatusBadge status={task.status} />
          <PriorityBadge priority={task.priority} />
          {task.assignedTo && <span className="calendar-chip__assignee-name">{task.assignedTo.name}</span>}
          {task.blockedByCount > 0 && (
            <span className="calendar-chip__blocked">
              <Link2 size={11} />
              Blocked
            </span>
          )}
        </div>
      </div>
    );
  }

  return (
    <div
      {...commonProps}
      className={`calendar-chip calendar-chip--compact calendar-chip--priority-${task.priority.toLowerCase()}${isDragging ? ' calendar-chip--dragging' : ''}${hasError ? ' calendar-chip--error' : ''}`}
    >
      <span className={`calendar-chip__dot calendar-chip__dot--${task.status.toLowerCase()}`} />
      <span className="calendar-chip__title">{task.title}</span>
      {task.blockedByCount > 0 && <Link2 size={10} className="calendar-chip__blocked-icon" />}
      {task.assignedTo && <span className="calendar-chip__avatar">{initials(task.assignedTo.name)}</span>}
    </div>
  );
}
