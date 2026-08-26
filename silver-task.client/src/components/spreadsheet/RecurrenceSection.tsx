import { useState } from 'react';
import { Repeat } from 'lucide-react';
import type { Task } from '@/types/task';
import type { UserSummary } from '@/types/project';
import { useRecurrenceRule, useResumeRecurrence, useStopRecurrence } from '@/hooks/useRecurrence';
import { RecurrenceFormDialog } from './RecurrenceFormDialog';
import { RecurrenceSeriesDialog } from './RecurrenceSeriesDialog';
import { formatDate } from '@/utils/formatDate';
import './DependenciesSection.css';
import './RecurrenceSection.css';

interface RecurrenceSectionProps {
  task: Task;
  projectId: string;
  members: UserSummary[];
  onOpenDetail: (taskId: string) => void;
  canEdit: boolean;
}

export function RecurrenceSection({ task, projectId, members, onOpenDetail, canEdit }: RecurrenceSectionProps) {
  const { data: rule, isLoading } = useRecurrenceRule(task.id);
  const stopRecurrence = useStopRecurrence(projectId);
  const resumeRecurrence = useResumeRecurrence(projectId);
  const [showForm, setShowForm] = useState<'create' | 'edit' | null>(null);
  const [showSeries, setShowSeries] = useState(false);

  return (
    <div className="task-detail-panel__section">
      <div className="recurrence-section__header">
        <h3>Recurring Task</h3>
        {!isLoading && !rule && canEdit && (
          <button type="button" className="subtasks-section__add" onClick={() => setShowForm('create')}>
            Make Recurring
          </button>
        )}
      </div>

      {!isLoading && !rule && <p className="dependencies-section__empty">This task does not repeat.</p>}

      {rule && (
        <div className="recurrence-section__summary">
          <div className="recurrence-section__schedule">
            <Repeat size={14} />
            <span>{rule.scheduleDescription}</span>
            {!rule.isActive && <span className="recurrence-section__stopped-badge">Stopped</span>}
          </div>
          {rule.isActive && rule.nextOccurrenceDate && (
            <p className="recurrence-section__next">Next occurrence: {formatDate(rule.nextOccurrenceDate)}</p>
          )}
          <div className="recurrence-section__actions">
            {canEdit && (
              <button type="button" className="recurrence-section__action" onClick={() => setShowForm('edit')}>
                Edit Recurrence
              </button>
            )}
            {canEdit &&
              (rule.isActive ? (
                <button
                  type="button"
                  className="recurrence-section__action"
                  onClick={() => stopRecurrence.mutate(task.id)}
                  disabled={stopRecurrence.isPending}
                >
                  {stopRecurrence.isPending ? 'Stopping...' : 'Stop Recurrence'}
                </button>
              ) : (
                <button
                  type="button"
                  className="recurrence-section__action"
                  onClick={() => resumeRecurrence.mutate(task.id)}
                  disabled={resumeRecurrence.isPending}
                >
                  {resumeRecurrence.isPending ? 'Resuming...' : 'Resume Recurrence'}
                </button>
              ))}
            <button type="button" className="recurrence-section__action" onClick={() => setShowSeries(true)}>
              View Series
            </button>
          </div>
        </div>
      )}

      {showForm && (
        <RecurrenceFormDialog
          projectId={projectId}
          members={members}
          mode={showForm}
          task={task}
          existingRule={rule ?? undefined}
          onClose={() => setShowForm(null)}
        />
      )}

      {showSeries && <RecurrenceSeriesDialog taskId={task.id} onOpenDetail={onOpenDetail} onClose={() => setShowSeries(false)} />}
    </div>
  );
}
