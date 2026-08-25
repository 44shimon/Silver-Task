import type { Task } from '@/types/task';
import { getTaskAncestors } from '@/utils/taskHierarchy';
import './TaskBreadcrumb.css';

interface TaskBreadcrumbProps {
  task: Task;
  /** The full (unfiltered) project task list — needed to walk parentTaskId links. */
  tasks: Task[];
  onOpenDetail: (taskId: string) => void;
}

// Subsumes the simpler "Parent Task: X" display — the full ancestor chain already includes the
// immediate parent as its last (clickable) entry, so one component covers both spec sections.
// Renders nothing for a top-level task (no ancestors to show).
export function TaskBreadcrumb({ task, tasks, onOpenDetail }: TaskBreadcrumbProps) {
  const ancestors = getTaskAncestors(task, tasks);
  if (ancestors.length === 0) {
    return null;
  }

  return (
    <nav className="task-breadcrumb" aria-label="Task hierarchy">
      {ancestors.map((ancestor) => (
        <span className="task-breadcrumb__item" key={ancestor.id}>
          <button type="button" className="task-breadcrumb__link" onClick={() => onOpenDetail(ancestor.id)}>
            {ancestor.title}
          </button>
          <span className="task-breadcrumb__separator">›</span>
        </span>
      ))}
      <span className="task-breadcrumb__current">{task.title}</span>
    </nav>
  );
}
