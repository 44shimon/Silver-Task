import { useState, type DragEvent } from 'react';
import type { Task, TaskStatus } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { groupTasksByStatus } from '@/utils/kanbanGrouping';
import { KanbanColumn } from './KanbanColumn';
import './KanbanBoard.css';

interface KanbanBoardProps {
  projectId: string;
  /** Already-filtered/sorted tasks (same `filteredTasks` the Table view renders) — the two
   * views read from identical data, just visualized differently. Sort order is preserved
   * within each column since grouping is a plain filter over this already-sorted array. */
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
}

// Reuses useUpdateTask exactly as every dropdown cell in the Table view does: same optimistic
// update, same rollback on failure, same underlying PUT /api/tasks/{id} — a drag-drop status
// change is not a different kind of edit than picking Status from the table's dropdown.
export function KanbanBoard({ projectId, tasks, onOpenDetail }: KanbanBoardProps) {
  const updateTask = useUpdateTask(projectId);
  const [draggingTaskId, setDraggingTaskId] = useState<string | null>(null);
  const [dragOverColumnId, setDragOverColumnId] = useState<string | null>(null);

  const columns = groupTasksByStatus(tasks);
  const errorTaskId = updateTask.isError ? (updateTask.variables?.task.id ?? null) : null;

  function handleDrop(taskId: string, statusId: string) {
    setDragOverColumnId(null);
    const task = tasks.find((t) => t.id === taskId);
    if (!task || task.status === statusId) {
      return;
    }
    updateTask.mutate({ task, change: taskFieldChange.status(statusId as TaskStatus) });
  }

  return (
    <div className="kanban-board">
      {columns.map((column) => (
        <KanbanColumn
          key={column.id}
          column={column}
          isDragOver={dragOverColumnId === column.id}
          draggingTaskId={draggingTaskId}
          errorTaskId={errorTaskId}
          onCardDragStart={setDraggingTaskId}
          onCardDragEnd={() => setDraggingTaskId(null)}
          onDragEnter={() => setDragOverColumnId(column.id)}
          onDragLeave={(event: DragEvent<HTMLDivElement>) => {
            if (!event.currentTarget.contains(event.relatedTarget as Node)) {
              setDragOverColumnId((current) => (current === column.id ? null : current));
            }
          }}
          onDrop={(taskId) => handleDrop(taskId, column.id)}
          onOpenDetail={onOpenDetail}
        />
      ))}
    </div>
  );
}
