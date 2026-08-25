import type { Task } from '@/types/task';
import './DependencySummaryCell.css';

interface DependencySummaryCellProps {
  task: Task;
}

// Compact per spec ("Do not make the table unnecessarily wide") — full dependency detail lives
// in the Task Detail panel's Dependencies section; this is just enough to notice at a glance.
export function DependencySummaryCell({ task }: DependencySummaryCellProps) {
  if (task.blockedByCount > 0) {
    return (
      <span className="dependency-summary dependency-summary--blocked">
        Blocked by {task.blockedByCount}
      </span>
    );
  }

  const parts: string[] = [];
  if (task.dependsOnCount > 0) {
    parts.push(`Depends on ${task.dependsOnCount}`);
  }
  if (task.dependentCount > 0) {
    parts.push(`Blocking ${task.dependentCount}`);
  }

  if (parts.length === 0) {
    return <span className="dependency-summary dependency-summary--none">—</span>;
  }

  return <span className="dependency-summary">{parts.join(' · ')}</span>;
}
