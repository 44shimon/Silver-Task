import type { DragEvent } from 'react';
import type { KanbanColumn as KanbanColumnData } from '@/utils/kanbanGrouping';
import { KanbanCard } from './KanbanCard';
import './KanbanColumn.css';

interface KanbanColumnProps {
  column: KanbanColumnData;
  isDragOver: boolean;
  draggingTaskId: string | null;
  errorTaskId: string | null;
  canEdit: boolean;
  onCardDragStart: (taskId: string) => void;
  onCardDragEnd: () => void;
  onDragEnter: () => void;
  onDragLeave: (event: DragEvent<HTMLDivElement>) => void;
  onDrop: (taskId: string) => void;
  onOpenDetail: (taskId: string) => void;
}

export function KanbanColumn({
  column,
  isDragOver,
  draggingTaskId,
  errorTaskId,
  canEdit,
  onCardDragStart,
  onCardDragEnd,
  onDragEnter,
  onDragLeave,
  onDrop,
  onOpenDetail,
}: KanbanColumnProps) {
  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    if (!canEdit) {
      return;
    }
    const taskId = event.dataTransfer.getData('text/plain');
    if (taskId) {
      onDrop(taskId);
    }
  }

  return (
    <div
      className={`kanban-column${isDragOver ? ' kanban-column--drag-over' : ''}`}
      onDragOver={(e) => e.preventDefault()}
      onDragEnter={onDragEnter}
      onDragLeave={onDragLeave}
      onDrop={handleDrop}
    >
      <div className="kanban-column__header">
        <span className="kanban-column__title">{column.label}</span>
        <span className="kanban-column__count">{column.tasks.length}</span>
      </div>

      <div className="kanban-column__cards">
        {column.tasks.map((task) => (
          <KanbanCard
            key={task.id}
            task={task}
            isDragging={draggingTaskId === task.id}
            hasError={errorTaskId === task.id}
            canEdit={canEdit}
            onDragStart={() => onCardDragStart(task.id)}
            onDragEnd={onCardDragEnd}
            onOpenDetail={() => onOpenDetail(task.id)}
          />
        ))}
        {column.tasks.length === 0 && <div className="kanban-column__empty">No tasks</div>}
      </div>
    </div>
  );
}
