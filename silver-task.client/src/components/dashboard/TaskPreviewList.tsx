import { useNavigate } from 'react-router-dom';
import type { Task } from '@/types/task';
import { StatusBadge } from '@/components/spreadsheet/StatusBadge';
import { PriorityBadge } from '@/components/spreadsheet/PriorityBadge';
import { formatDate } from '@/utils/formatDate';
import './TaskPreviewList.css';

interface TaskPreviewListProps {
  tasks: Task[];
  /** "due" (default) shows the due date; "completed" shows the completion date instead. */
  dateField?: 'due' | 'completed';
  showStatus?: boolean;
}

// Compact preview rows shared by Overdue/Due Today/Upcoming/Recently Completed — deliberately
// not the full task table (the spec's own "do not duplicate the full task table" instruction);
// clicking opens the existing Task Detail panel via the same ?task=<id> deep-link convention every
// other task-opening surface in this app already uses (GlobalSearch, NotificationItem, ...).
export function TaskPreviewList({ tasks, dateField = 'due', showStatus }: TaskPreviewListProps) {
  const navigate = useNavigate();

  function open(task: Task) {
    navigate(`/projects/${task.projectId}?task=${task.id}`);
  }

  return (
    <ul className="task-preview-list">
      {tasks.map((task) => {
        const dateValue = dateField === 'completed' ? task.completedAt : task.dueDate;
        return (
          <li key={task.id}>
            <button type="button" className="task-preview-list__row" onClick={() => open(task)}>
              <div className="task-preview-list__main">
                <span className="task-preview-list__title">{task.title}</span>
                <span className="task-preview-list__project">{task.projectName}</span>
              </div>
              <div className="task-preview-list__meta">
                {showStatus ? <StatusBadge status={task.status} /> : <PriorityBadge priority={task.priority} />}
                {dateValue && <span className="task-preview-list__date">{formatDate(dateValue)}</span>}
              </div>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
