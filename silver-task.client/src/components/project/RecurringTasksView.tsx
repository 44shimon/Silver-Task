import { useState } from 'react';
import { Repeat } from 'lucide-react';
import type { Task } from '@/types/task';
import type { UserSummary } from '@/types/project';
import type { RecurrenceRule } from '@/types/recurrence';
import { useProjectRecurringTasks, useResumeRecurrence, useStopRecurrence } from '@/hooks/useRecurrence';
import { RecurrenceFormDialog } from '../spreadsheet/RecurrenceFormDialog';
import { RecurrenceSeriesDialog } from '../spreadsheet/RecurrenceSeriesDialog';
import { formatDate } from '@/utils/formatDate';
import './RecurringTasksView.css';

interface RecurringTasksViewProps {
  projectId: string;
  tasks: Task[];
  members: UserSummary[];
  onOpenDetail: (taskId: string) => void;
  canEdit: boolean;
}

// Shows recurrence *rules*, not task occurrences — a deliberately separate data source
// (GET /projects/{id}/recurring-tasks) from the filteredTasks every other view tab shares, since
// a rule isn't itself a Task the spreadsheet grid knows how to render.
export function RecurringTasksView({ projectId, tasks, members, onOpenDetail, canEdit }: RecurringTasksViewProps) {
  const { data: rules, isLoading } = useProjectRecurringTasks(projectId);
  const stopRecurrence = useStopRecurrence(projectId);
  const resumeRecurrence = useResumeRecurrence(projectId);
  const [editingRule, setEditingRule] = useState<RecurrenceRule | null>(null);
  const [viewingSeriesTaskId, setViewingSeriesTaskId] = useState<string | null>(null);

  if (isLoading) {
    return <p>Loading recurring tasks...</p>;
  }

  if (!rules || rules.length === 0) {
    return (
      <div className="recurring-tasks-view__empty">
        <Repeat size={20} />
        <p>No recurring tasks yet. Open any task and choose &ldquo;Make Recurring&rdquo; to start a series.</p>
      </div>
    );
  }

  return (
    <div className="recurring-tasks-view">
      <table className="recurring-tasks-view__table">
        <thead>
          <tr>
            <th>Task</th>
            <th>Schedule</th>
            <th>Assigned To</th>
            <th>Next Occurrence</th>
            <th>Ends</th>
            <th>Status</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rules.map((rule) => {
            const templateTask = tasks.find((t) => t.id === rule.templateTaskId);
            return (
              <tr key={rule.id}>
                <td>
                  {templateTask ? (
                    <button type="button" className="recurring-tasks-view__link" onClick={() => onOpenDetail(templateTask.id)}>
                      {rule.title}
                    </button>
                  ) : (
                    rule.title
                  )}
                </td>
                <td>{rule.scheduleDescription}</td>
                <td>{rule.assignedTo?.name ?? 'Unassigned'}</td>
                <td>{rule.nextOccurrenceDate ? formatDate(rule.nextOccurrenceDate) : '—'}</td>
                <td>{rule.endDate ? formatDate(rule.endDate) : rule.maxOccurrences ? `After ${rule.maxOccurrences}` : 'Never'}</td>
                <td>
                  <span className={`recurring-tasks-view__status${rule.isActive ? '' : ' recurring-tasks-view__status--stopped'}`}>
                    {rule.isActive ? 'Active' : 'Stopped'}
                  </span>
                </td>
                <td>
                  <div className="recurring-tasks-view__actions">
                    {templateTask && canEdit && (
                      <button type="button" onClick={() => setEditingRule(rule)}>
                        Edit
                      </button>
                    )}
                    {canEdit &&
                      (rule.isActive ? (
                        <button
                          type="button"
                          onClick={() => rule.templateTaskId && stopRecurrence.mutate(rule.templateTaskId)}
                          disabled={!rule.templateTaskId || stopRecurrence.isPending}
                        >
                          Stop
                        </button>
                      ) : (
                        <button
                          type="button"
                          onClick={() => rule.templateTaskId && resumeRecurrence.mutate(rule.templateTaskId)}
                          disabled={!rule.templateTaskId || resumeRecurrence.isPending}
                        >
                          Resume
                        </button>
                      ))}
                    {rule.templateTaskId && (
                      <button type="button" onClick={() => setViewingSeriesTaskId(rule.templateTaskId)}>
                        View Series
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {editingRule && (() => {
        const templateTask = tasks.find((t) => t.id === editingRule.templateTaskId);
        return templateTask ? (
          <RecurrenceFormDialog
            projectId={projectId}
            members={members}
            mode="edit"
            task={templateTask}
            existingRule={editingRule}
            onClose={() => setEditingRule(null)}
          />
        ) : null;
      })()}

      {viewingSeriesTaskId && (
        <RecurrenceSeriesDialog
          taskId={viewingSeriesTaskId}
          onOpenDetail={onOpenDetail}
          onClose={() => setViewingSeriesTaskId(null)}
        />
      )}
    </div>
  );
}
