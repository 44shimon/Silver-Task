import type { DragEvent, KeyboardEvent } from 'react';
import { Link2 } from 'lucide-react';
import type { Task } from '@/types/task';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import { formatDate } from '@/utils/formatDate';
import { initials } from '@/utils/initials';
import './KanbanCard.css';

interface KanbanCardProps {
  task: Task;
  isDragging: boolean;
  hasError: boolean;
  onDragStart: () => void;
  onDragEnd: () => void;
  onOpenDetail: () => void;
}

// Native HTML5 drag-and-drop (no new dependency) — `draggable` + a click handler coexist fine
// here since the card has no inline-edit affordance of its own to conflict with (unlike a
// spreadsheet cell), so clicking anywhere on the card to open its detail is unambiguous.
export function KanbanCard({ task, isDragging, hasError, onDragStart, onDragEnd, onOpenDetail }: KanbanCardProps) {
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

  return (
    <div
      className={`kanban-card${isDragging ? ' kanban-card--dragging' : ''}${hasError ? ' kanban-card--error' : ''}`}
      draggable
      onDragStart={handleDragStart}
      onDragEnd={onDragEnd}
      onClick={onOpenDetail}
      role="button"
      tabIndex={0}
      onKeyDown={handleKeyDown}
      title={hasError ? 'Could not save the move — try dragging again' : undefined}
    >
      <div className="kanban-card__title">{task.title}</div>

      <div className="kanban-card__meta">
        <PriorityBadge priority={task.priority} />
        {task.dueDate && <span className="kanban-card__due">{formatDate(task.dueDate)}</span>}
      </div>

      {task.blockedByCount > 0 && (
        // The whole card already opens the task detail (Dependencies section included) on click —
        // this indicator doesn't need its own click handler, just to be visible.
        <div className="kanban-card__blocked">
          <Link2 size={11} />
          <span>Blocked by {task.blockedByCount} task{task.blockedByCount === 1 ? '' : 's'}</span>
        </div>
      )}

      {task.assignedTo && (
        <div className="kanban-card__assignee">
          <span className="kanban-card__avatar">{initials(task.assignedTo.name)}</span>
          <span className="kanban-card__assignee-name">{task.assignedTo.name}</span>
        </div>
      )}
    </div>
  );
}
