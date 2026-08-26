import { useState } from 'react';
import type { Task, TaskPriority } from '@/types/task';
import { PRIORITY_OPTIONS } from '@/types/task';
import type { UserSummary } from '@/types/project';
import type {
  RecurrenceEditScope,
  RecurrenceFrequency,
  RecurrenceRule,
  WeekdayName,
} from '@/types/recurrence';
import { RECURRENCE_FREQUENCY_OPTIONS, WEEKDAY_LABELS, WEEKDAY_OPTIONS } from '@/types/recurrence';
import { useCreateRecurrence, useUpdateRecurrence } from '@/hooks/useRecurrence';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import '@/components/shared/ConfirmDeleteDialog.css';
import '@/pages/settings/SettingsForm.css';
import './RecurrenceFormDialog.css';

type EndMode = 'never' | 'onDate' | 'afterCount';

interface RecurrenceFormDialogProps {
  projectId: string;
  members: UserSummary[];
  onClose: () => void;
  /** "create" attaches a new rule to `task` (which becomes the series' first occurrence).
   * "edit" changes an existing rule reached via `task` (any occurrence in the series works —
   * the backend resolves the rule from it). */
  mode: 'create' | 'edit';
  task: Task;
  existingRule?: RecurrenceRule;
}

// One dialog for both Create and Edit — the field set is identical (title/description/priority/
// assignee/schedule); only the submit action and the presence of the This-and-future/Entire-series
// scope choice differ. See CreateRecurrenceRequest/UpdateRecurrenceRequest on the backend, which
// share the same rule-field shape for the same reason.
export function RecurrenceFormDialog({ projectId, members, onClose, mode, task, existingRule }: RecurrenceFormDialogProps) {
  const createRecurrence = useCreateRecurrence(projectId);
  const updateRecurrence = useUpdateRecurrence(projectId);
  const isPending = createRecurrence.isPending || updateRecurrence.isPending;

  const [title, setTitle] = useState(existingRule?.title ?? task.title);
  const [description, setDescription] = useState(existingRule?.description ?? task.description ?? '');
  const [priority, setPriority] = useState<TaskPriority>(existingRule?.priority ?? task.priority);
  const [assigneeId, setAssigneeId] = useState(existingRule?.assignedTo?.id ?? task.assignedTo?.id ?? '');

  const [frequency, setFrequency] = useState<RecurrenceFrequency>(existingRule?.frequency ?? 'Weekly');
  const [interval, setIntervalValue] = useState(existingRule?.interval ?? 1);
  const [daysOfWeek, setDaysOfWeek] = useState<Set<WeekdayName>>(
    new Set(existingRule?.daysOfWeek?.length ? existingRule.daysOfWeek : [todayWeekday(task.startDate ?? task.dueDate)]),
  );
  const [dayOfMonth, setDayOfMonth] = useState(existingRule?.dayOfMonth ?? dayOfMonthFrom(task.startDate ?? task.dueDate));
  const [monthOfYear, setMonthOfYear] = useState(existingRule?.monthOfYear ?? monthFrom(task.startDate ?? task.dueDate));

  const [startDate, setStartDate] = useState(existingRule?.startDate ?? task.startDate ?? task.dueDate ?? todayDateOnly());
  const [endMode, setEndMode] = useState<EndMode>(
    existingRule?.endDate ? 'onDate' : existingRule?.maxOccurrences ? 'afterCount' : 'never',
  );
  const [endDate, setEndDate] = useState(existingRule?.endDate ?? '');
  const [maxOccurrences, setMaxOccurrences] = useState(existingRule?.maxOccurrences ?? 10);

  const [scope, setScope] = useState<RecurrenceEditScope>('EntireSeries');

  const mutationError = createRecurrence.isError
    ? createRecurrence.error
    : updateRecurrence.isError
      ? updateRecurrence.error
      : null;
  const errorMessage = mutationError
    ? mutationError instanceof ApiError
      ? mutationError.message
      : 'Could not save recurrence.'
    : null;

  function toggleWeekday(day: WeekdayName) {
    setDaysOfWeek((prev) => {
      const next = new Set(prev);
      if (next.has(day)) {
        next.delete(day);
      } else {
        next.add(day);
      }
      return next;
    });
  }

  function applyWeekdayPreset() {
    setFrequency('Weekly');
    setIntervalValue(1);
    setDaysOfWeek(new Set(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']));
  }

  function handleSubmit() {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return;
    }

    const ruleFields = {
      title: trimmedTitle,
      description: description.trim() || undefined,
      priority,
      assignedToUserId: assigneeId || null,
      frequency,
      interval: Math.max(1, interval),
      daysOfWeek: frequency === 'Weekly' ? Array.from(daysOfWeek) : undefined,
      dayOfMonth: frequency === 'Monthly' || frequency === 'Yearly' ? dayOfMonth : undefined,
      monthOfYear: frequency === 'Yearly' ? monthOfYear : undefined,
      startDate,
      endDate: endMode === 'onDate' ? endDate || undefined : null,
      maxOccurrences: endMode === 'afterCount' ? maxOccurrences : null,
    };

    if (mode === 'create') {
      createRecurrence.mutate({ taskId: task.id, request: ruleFields }, { onSuccess: onClose });
    } else {
      updateRecurrence.mutate(
        {
          taskId: task.id,
          request: { ...ruleFields, scope, anchorOccurrenceDate: task.recurrenceOccurrenceDate },
        },
        { onSuccess: onClose },
      );
    }
  }

  const canSubmit = title.trim().length > 0 && (frequency !== 'Weekly' || daysOfWeek.size > 0);

  return (
    <Modal onClose={onClose} size="wide">
      <h2>{mode === 'create' ? 'Make Recurring' : 'Edit Recurrence'}</h2>

      {mode === 'edit' && (
        <div className="recurrence-form__scope">
          <p className="recurrence-form__scope-hint">
            To change only this occurrence, edit its fields directly in the task detail instead — this dialog changes
            the recurring rule itself.
          </p>
          <label className="recurrence-form__radio">
            <input
              type="radio"
              name="recurrence-scope"
              checked={scope === 'ThisAndFuture'}
              onChange={() => setScope('ThisAndFuture')}
            />
            This and future occurrences
          </label>
          <label className="recurrence-form__radio">
            <input
              type="radio"
              name="recurrence-scope"
              checked={scope === 'EntireSeries'}
              onChange={() => setScope('EntireSeries')}
            />
            Entire series
          </label>
        </div>
      )}

      <div className="settings-form__field">
        <label>Title</label>
        <input type="text" value={title} onChange={(e) => setTitle(e.target.value)} disabled={isPending} />
      </div>

      <div className="settings-form__field">
        <label>Description</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={2}
          disabled={isPending}
        />
      </div>

      <div className="recurrence-form__row">
        <div className="settings-form__field">
          <label>Priority</label>
          <select value={priority} onChange={(e) => setPriority(e.target.value as TaskPriority)} disabled={isPending}>
            {PRIORITY_OPTIONS.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="settings-form__field">
          <label>Assigned To</label>
          <select value={assigneeId} onChange={(e) => setAssigneeId(e.target.value)} disabled={isPending}>
            <option value="">Unassigned</option>
            {members.map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="recurrence-form__row">
        <div className="settings-form__field">
          <label>Recurrence</label>
          <select
            value={frequency}
            onChange={(e) => setFrequency(e.target.value as RecurrenceFrequency)}
            disabled={isPending}
          >
            {RECURRENCE_FREQUENCY_OPTIONS.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="settings-form__field">
          <label>Repeat every</label>
          <div className="recurrence-form__interval">
            <input
              type="number"
              min={1}
              max={365}
              value={interval}
              onChange={(e) => setIntervalValue(Math.max(1, Number(e.target.value) || 1))}
              disabled={isPending}
            />
            <span>{intervalUnitLabel(frequency, interval)}</span>
          </div>
        </div>
      </div>

      {frequency === 'Weekly' && (
        <div className="settings-form__field">
          <label>On</label>
          <div className="recurrence-form__weekdays">
            {WEEKDAY_OPTIONS.map((day) => (
              <label key={day} className="recurrence-form__weekday">
                <input type="checkbox" checked={daysOfWeek.has(day)} onChange={() => toggleWeekday(day)} disabled={isPending} />
                {WEEKDAY_LABELS[day]}
              </label>
            ))}
          </div>
          <button type="button" className="recurrence-form__preset" onClick={applyWeekdayPreset} disabled={isPending}>
            Every weekday
          </button>
        </div>
      )}

      {(frequency === 'Monthly' || frequency === 'Yearly') && (
        <div className="recurrence-form__row">
          {frequency === 'Yearly' && (
            <div className="settings-form__field">
              <label>Month</label>
              <select value={monthOfYear} onChange={(e) => setMonthOfYear(Number(e.target.value))} disabled={isPending}>
                {MONTH_NAMES.map((name, index) => (
                  <option key={name} value={index + 1}>
                    {name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="settings-form__field">
            <label>Day of month</label>
            <input
              type="number"
              min={1}
              max={31}
              value={dayOfMonth}
              onChange={(e) => setDayOfMonth(Math.min(31, Math.max(1, Number(e.target.value) || 1)))}
              disabled={isPending}
            />
          </div>
        </div>
      )}

      <div className="settings-form__field">
        <label>Starts</label>
        <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} disabled={isPending} />
      </div>

      <div className="settings-form__field">
        <label>Ends</label>
        <div className="recurrence-form__end-options">
          <label className="recurrence-form__radio">
            <input type="radio" name="recurrence-end" checked={endMode === 'never'} onChange={() => setEndMode('never')} />
            Never
          </label>
          <label className="recurrence-form__radio">
            <input type="radio" name="recurrence-end" checked={endMode === 'onDate'} onChange={() => setEndMode('onDate')} />
            On date
            <input
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              disabled={isPending || endMode !== 'onDate'}
            />
          </label>
          <label className="recurrence-form__radio">
            <input
              type="radio"
              name="recurrence-end"
              checked={endMode === 'afterCount'}
              onChange={() => setEndMode('afterCount')}
            />
            After
            <input
              type="number"
              min={1}
              max={1000}
              value={maxOccurrences}
              onChange={(e) => setMaxOccurrences(Math.min(1000, Math.max(1, Number(e.target.value) || 1)))}
              disabled={isPending || endMode !== 'afterCount'}
            />
            occurrences
          </label>
        </div>
      </div>

      {errorMessage && <p className="form-error">{errorMessage}</p>}

      <div className="move-task-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={isPending}>
          Cancel
        </button>
        <button type="button" className="settings-form__save" onClick={handleSubmit} disabled={isPending || !canSubmit}>
          {isPending ? 'Saving...' : mode === 'create' ? 'Make Recurring' : 'Save Changes'}
        </button>
      </div>
    </Modal>
  );
}

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

function todayDateOnly(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

function todayWeekday(dateOnly: string | null): WeekdayName {
  const date = dateOnly ? parseDateOnlyLocal(dateOnly) : new Date();
  return WEEKDAY_OPTIONS[date.getDay()];
}

function dayOfMonthFrom(dateOnly: string | null): number {
  return dateOnly ? parseDateOnlyLocal(dateOnly).getDate() : new Date().getDate();
}

function monthFrom(dateOnly: string | null): number {
  return (dateOnly ? parseDateOnlyLocal(dateOnly).getMonth() : new Date().getMonth()) + 1;
}

function parseDateOnlyLocal(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function intervalUnitLabel(frequency: RecurrenceFrequency, interval: number): string {
  const unit = { Daily: 'day', Weekly: 'week', Monthly: 'month', Yearly: 'year' }[frequency];
  return interval === 1 ? `${unit}(s)` : `${unit}s`;
}
