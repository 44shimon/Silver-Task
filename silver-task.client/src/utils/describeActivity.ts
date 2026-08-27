import { STATUS_LABELS, type TaskStatus } from '@/types/task';
import type { TaskActivity } from '@/types/activity';
import { formatDate } from '@/utils/formatDate';

// Shared "what happened" phrasing for a TaskActivity row — used by both ActivityHistorySection
// (one task's own history) and the dashboard's Recent/My Activity widgets (Phase 37, a
// cross-project feed of the same underlying rows). Lives in utils/, not a component file, so
// importing it doesn't drag react/only-export-components into either consumer.
export function describeActivity(activity: TaskActivity): string {
  const actor = activity.user?.name ?? 'Someone';

  if (activity.action === 'Created') {
    return `${actor} created this task`;
  }

  if (activity.action === 'Assigned') {
    return activity.newValue
      ? `${actor} assigned this task to ${activity.newValue}`
      : `${actor} unassigned this task`;
  }

  if (activity.action === 'AttachmentAdded') {
    return `${actor} attached ${activity.newValue ?? 'a file'}`;
  }

  if (activity.action === 'AttachmentRemoved') {
    return `${actor} removed attachment ${activity.oldValue ?? ''}`.trim();
  }

  if (activity.action === 'DependencyAdded') {
    return `${actor} made "${activity.newValue ?? 'a task'}" a dependency of this task`;
  }

  if (activity.action === 'DependencyRemoved') {
    return `${actor} removed the dependency on "${activity.oldValue ?? 'a task'}"`;
  }

  if (activity.action === 'SubtaskAdded') {
    return `${actor} added subtask "${activity.newValue ?? 'a task'}"`;
  }

  if (activity.action === 'Moved') {
    return activity.newValue && activity.newValue !== 'Top Level'
      ? `${actor} moved this task under "${activity.newValue}"`
      : `${actor} moved this task to top level`;
  }

  if (activity.action === 'Reordered') {
    return `${actor} reordered this task among its siblings`;
  }

  if (activity.action === 'RecurrenceCreated') {
    return `${actor} set up a recurring task: ${activity.newValue ?? 'schedule set'}`;
  }

  if (activity.action === 'RecurrenceEdited') {
    return `${actor} changed the recurrence from "${activity.oldValue ?? '?'}" to "${activity.newValue ?? '?'}"`;
  }

  if (activity.action === 'RecurrenceStopped') {
    return `${actor} stopped this recurring series`;
  }

  if (activity.action === 'RecurrenceResumed') {
    return `${actor} resumed this recurring series`;
  }

  if (activity.action === 'RecurrenceDeleted') {
    return `${actor} deleted this recurring series`;
  }

  if (activity.action === 'RecurringOccurrenceGenerated') {
    return `New occurrence generated automatically for "${activity.newValue ?? 'this series'}"`;
  }

  const field = activity.fieldName ?? 'a field';
  const oldDisplay = formatActivityValue(activity.fieldName, activity.oldValue);
  const newDisplay = formatActivityValue(activity.fieldName, activity.newValue);
  return `${actor} changed ${field} from ${oldDisplay} to ${newDisplay}`;
}

function formatActivityValue(fieldName: string | null, value: string | null): string {
  if (!value) {
    return '(none)';
  }
  if (fieldName === 'Status') {
    return STATUS_LABELS[value as TaskStatus] ?? value;
  }
  if (fieldName === 'Start Date' || fieldName === 'Due Date') {
    return formatDate(value);
  }
  return value;
}
