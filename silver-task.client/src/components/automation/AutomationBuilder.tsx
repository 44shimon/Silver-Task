import { useState } from 'react';
import { Plus, X } from 'lucide-react';
import type { UserSummary } from '@/types/project';
import type { CustomField } from '@/types/customField';
import { STATUS_LABELS, STATUS_OPTIONS, PRIORITY_OPTIONS } from '@/types/task';
import {
  ACTION_TYPE_LABELS,
  ACTION_TYPE_OPTIONS,
  CUSTOM_FIELD_PREFIX,
  FIELD_LABELS,
  OPERATOR_LABELS,
  OPERATOR_OPTIONS,
  OPERATORS_WITHOUT_VALUE,
  SUBTASK_ALL_COMPLETE_FIELD,
  TEMPLATE_VARIABLES,
  TRIGGER_TYPE_LABELS,
  TRIGGER_TYPE_OPTIONS,
  USER_SELECTOR_LABELS,
  getApplicableFields,
  type Automation,
  type AutomationActionRequest,
  type AutomationActionType,
  type AutomationConditionRequest,
  type AutomationTriggerType,
  type AutomationUserSelector,
} from '@/types/automation';
import {
  useCreateGlobalAutomation,
  useCreateProjectAutomation,
  useTestAutomation,
  useUpdateAutomation,
} from '@/hooks/useAutomations';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import '@/pages/settings/SettingsForm.css';
import '@/components/shared/ConfirmDeleteDialog.css';
import './AutomationBuilder.css';

interface AutomationBuilderProps {
  /** Null for a global (Administrator-only) automation. */
  projectId: string | null;
  /** Project members for project automations, or every active user for global ones — used for
   * the "specific person" pickers in Assign Task / Create Task / Send Notification. */
  users: UserSummary[];
  /** Empty for global automations — custom field conditions need a fixed project to resolve
   * against, so they're only offered when building a project-scoped automation. */
  customFields: CustomField[];
  automation?: Automation | null;
  onClose: () => void;
}

function defaultParameters(actionType: AutomationActionType): Record<string, unknown> {
  switch (actionType) {
    case 'AssignTask':
      return { assignMode: 'ProjectManager', targetUserId: null };
    case 'ChangeStatus':
      return { newStatus: 'InProgress' };
    case 'ChangePriority':
      return { newPriority: 'High' };
    case 'AddLabel':
    case 'RemoveLabel':
    case 'AddFileTag':
      return { tagName: '' };
    case 'SetDueDate':
    case 'SetStartDate':
      return { offsetDays: 0, clearDate: false };
    case 'AddComment':
      return { commentTemplate: '' };
    case 'CreateTask':
      return {
        titleTemplate: '',
        descriptionTemplate: '',
        assignMode: 'None',
        targetUserId: null,
        status: 'NotStarted',
        priority: 'Medium',
        dueDateOffsetDays: null,
      };
    case 'SendNotification':
      return { recipientMode: 'TaskAssignee', targetUserId: null, messageTemplate: '' };
  }
}

export function AutomationBuilder({ projectId, users, customFields, automation, onClose }: AutomationBuilderProps) {
  const isEdit = !!automation;
  const createProject = useCreateProjectAutomation(projectId ?? '');
  const createGlobal = useCreateGlobalAutomation();
  const update = useUpdateAutomation(projectId ?? undefined);
  const test = useTestAutomation(automation?.id ?? '');
  const isPending = createProject.isPending || createGlobal.isPending || update.isPending;

  const [name, setName] = useState(automation?.name ?? '');
  const [description, setDescription] = useState(automation?.description ?? '');
  const [triggerType, setTriggerType] = useState<AutomationTriggerType>(automation?.triggerType ?? 'TaskCreated');
  const [isActive, setIsActive] = useState(automation?.isActive ?? true);
  const [conditions, setConditions] = useState<AutomationConditionRequest[]>(
    automation?.conditions.map((c) => ({ field: c.field, operator: c.operator, value: c.value })) ?? [],
  );
  const [actions, setActions] = useState<AutomationActionRequest[]>(
    automation?.actions.map((a) => ({ actionType: a.actionType, parameters: a.parameters })) ?? [],
  );
  const [sampleEntityId, setSampleEntityId] = useState('');

  const availableFields = [
    ...getApplicableFields(triggerType),
    ...(projectId && !triggerType.startsWith('File') && triggerType !== 'ProjectCreated'
      ? customFields.map((f) => `${CUSTOM_FIELD_PREFIX}${f.id}`)
      : []),
  ];

  function fieldLabel(field: string): string {
    if (field.startsWith(CUSTOM_FIELD_PREFIX)) {
      const id = field.slice(CUSTOM_FIELD_PREFIX.length);
      return customFields.find((f) => f.id === id)?.name ?? field;
    }
    return FIELD_LABELS[field] ?? field;
  }

  function addCondition() {
    setConditions((prev) => [...prev, { field: availableFields[0] ?? '', operator: 'Equals', value: '' }]);
  }

  function updateCondition(index: number, patch: Partial<AutomationConditionRequest>) {
    setConditions((prev) => prev.map((c, i) => (i === index ? { ...c, ...patch } : c)));
  }

  function removeCondition(index: number) {
    setConditions((prev) => prev.filter((_, i) => i !== index));
  }

  function addAction() {
    setActions((prev) => [...prev, { actionType: 'ChangeStatus', parameters: defaultParameters('ChangeStatus') }]);
  }

  function updateAction(index: number, patch: Partial<AutomationActionRequest>) {
    setActions((prev) => prev.map((a, i) => (i === index ? { ...a, ...patch } : a)));
  }

  function updateActionParam(index: number, key: string, value: unknown) {
    setActions((prev) => prev.map((a, i) => (i === index ? { ...a, parameters: { ...a.parameters, [key]: value } } : a)));
  }

  function removeAction(index: number) {
    setActions((prev) => prev.filter((_, i) => i !== index));
  }

  const mutationError = createProject.isError
    ? createProject.error
    : createGlobal.isError
      ? createGlobal.error
      : update.isError
        ? update.error
        : null;
  const errorMessage = mutationError
    ? mutationError instanceof ApiError
      ? mutationError.message
      : 'Could not save automation.'
    : null;

  const canSubmit = name.trim().length > 0 && actions.length > 0;

  function handleSubmit() {
    const request = {
      name: name.trim(),
      description: description.trim() || undefined,
      triggerType,
      isActive,
      conditions: conditions.filter((c) => c.field),
      actions,
    };

    if (isEdit && automation) {
      update.mutate({ id: automation.id, request }, { onSuccess: onClose });
    } else if (projectId) {
      createProject.mutate(request, { onSuccess: onClose });
    } else {
      createGlobal.mutate(request, { onSuccess: onClose });
    }
  }

  return (
    <Modal onClose={onClose} size="wide">
      <h2>{isEdit ? 'Edit Automation' : 'New Automation'}</h2>

      <div className="settings-form__field">
        <label>Name</label>
        <input type="text" value={name} onChange={(e) => setName(e.target.value)} disabled={isPending} placeholder="e.g. High Priority Assignment" />
      </div>

      <div className="settings-form__field">
        <label>Description</label>
        <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} disabled={isPending} />
      </div>

      <div className="settings-form__field">
        <label>Trigger</label>
        <select
          value={triggerType}
          onChange={(e) => {
            setTriggerType(e.target.value as AutomationTriggerType);
            setConditions([]);
          }}
          disabled={isPending}
        >
          {TRIGGER_TYPE_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {TRIGGER_TYPE_LABELS[option]}
            </option>
          ))}
        </select>
      </div>

      <label className="automation-builder__active-toggle">
        <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} disabled={isPending} />
        Active
      </label>

      <div className="automation-builder__section">
        <div className="automation-builder__section-header">
          <h3>Conditions</h3>
          <button type="button" className="automation-builder__add" onClick={addCondition} disabled={isPending}>
            <Plus size={13} />
            Add Condition
          </button>
        </div>
        <p className="automation-builder__hint">All conditions must match (AND). Leave empty to always run on this trigger.</p>

        {conditions.map((condition, index) => (
          <div className="automation-builder__row" key={index}>
            <select value={condition.field} onChange={(e) => updateCondition(index, { field: e.target.value })} disabled={isPending}>
              {availableFields.map((field) => (
                <option key={field} value={field}>
                  {fieldLabel(field)}
                </option>
              ))}
            </select>
            <select
              value={condition.operator}
              onChange={(e) => updateCondition(index, { operator: e.target.value as AutomationConditionRequest['operator'] })}
              disabled={isPending}
            >
              {OPERATOR_OPTIONS.map((op) => (
                <option key={op} value={op}>
                  {OPERATOR_LABELS[op]}
                </option>
              ))}
            </select>
            {!OPERATORS_WITHOUT_VALUE.has(condition.operator) && (
              <ConditionValueInput
                field={condition.field}
                value={condition.value ?? ''}
                users={users}
                disabled={isPending}
                onChange={(value) => updateCondition(index, { value })}
              />
            )}
            <button type="button" className="icon-button" aria-label="Remove condition" onClick={() => removeCondition(index)}>
              <X size={14} />
            </button>
          </div>
        ))}
      </div>

      <div className="automation-builder__section">
        <div className="automation-builder__section-header">
          <h3>Actions</h3>
          <button type="button" className="automation-builder__add" onClick={addAction} disabled={isPending}>
            <Plus size={13} />
            Add Action
          </button>
        </div>

        {actions.map((action, index) => (
          <div className="automation-builder__action-card" key={index}>
            <div className="automation-builder__row">
              <select
                value={action.actionType}
                onChange={(e) => {
                  const nextType = e.target.value as AutomationActionType;
                  updateAction(index, { actionType: nextType, parameters: defaultParameters(nextType) });
                }}
                disabled={isPending}
              >
                {ACTION_TYPE_OPTIONS.map((type) => (
                  <option key={type} value={type}>
                    {ACTION_TYPE_LABELS[type]}
                  </option>
                ))}
              </select>
              <button type="button" className="icon-button" aria-label="Remove action" onClick={() => removeAction(index)}>
                <X size={14} />
              </button>
            </div>
            <ActionParamsEditor
              actionType={action.actionType}
              parameters={action.parameters}
              users={users}
              disabled={isPending}
              onChange={(key, value) => updateActionParam(index, key, value)}
            />
          </div>
        ))}
        {actions.length === 0 && <p className="automation-builder__hint">Add at least one action.</p>}
      </div>

      {isEdit && automation && (
        <div className="automation-builder__section">
          <div className="automation-builder__section-header">
            <h3>Test</h3>
          </div>
          <p className="automation-builder__hint">
            Dry run only — evaluates conditions against a real entity and previews what actions would do. Nothing is written to the database.
          </p>
          <div className="automation-builder__row">
            <input
              type="text"
              placeholder="Sample task/file/project ID"
              value={sampleEntityId}
              onChange={(e) => setSampleEntityId(e.target.value)}
            />
            <button type="button" disabled={!sampleEntityId.trim() || test.isPending} onClick={() => test.mutate(sampleEntityId.trim())}>
              {test.isPending ? 'Testing...' : 'Run Test'}
            </button>
          </div>
          {test.data && (
            <div className="automation-builder__test-result">
              <p>{test.data.conditionsMatched ? 'Conditions matched.' : 'Conditions did not match — no actions would run.'}</p>
              {test.data.actionPreviews.map((preview, i) => (
                <p key={i}>• {preview}</p>
              ))}
            </div>
          )}
          {test.isError && (
            <p className="form-error">{test.error instanceof ApiError ? test.error.message : 'Could not run test.'}</p>
          )}
        </div>
      )}

      {errorMessage && <p className="form-error">{errorMessage}</p>}

      <div className="move-task-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={isPending}>
          Cancel
        </button>
        <button type="button" className="settings-form__save" onClick={handleSubmit} disabled={isPending || !canSubmit}>
          {isPending ? 'Saving...' : isEdit ? 'Save Changes' : 'Create Automation'}
        </button>
      </div>
    </Modal>
  );
}

interface ConditionValueInputProps {
  field: string;
  value: string;
  users: UserSummary[];
  disabled: boolean;
  onChange: (value: string) => void;
}

function ConditionValueInput({ field, value, users, disabled, onChange }: ConditionValueInputProps) {
  if (field === 'Task.Status') {
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled}>
        {STATUS_OPTIONS.map((s) => (
          <option key={s} value={s}>
            {STATUS_LABELS[s]}
          </option>
        ))}
      </select>
    );
  }
  if (field === 'Task.Priority') {
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled}>
        {PRIORITY_OPTIONS.map((p) => (
          <option key={p} value={p}>
            {p}
          </option>
        ))}
      </select>
    );
  }
  if (field === SUBTASK_ALL_COMPLETE_FIELD) {
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled}>
        <option value="true">True</option>
        <option value="false">False</option>
      </select>
    );
  }
  if (field === 'Task.DueDate' || field === 'Task.StartDate') {
    return <input type="date" value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled} />;
  }
  if (field === 'Task.AssigneeId' || field === 'Task.CreatorId' || field === 'File.UploadedByUserId' || field === 'Project.OwnerId') {
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled}>
        <option value="">Select a person...</option>
        {users.map((u) => (
          <option key={u.id} value={u.id}>
            {u.name}
          </option>
        ))}
      </select>
    );
  }
  return <input type="text" value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled} placeholder="Value" />;
}

interface ActionParamsEditorProps {
  actionType: AutomationActionType;
  parameters: Record<string, unknown>;
  users: UserSummary[];
  disabled: boolean;
  onChange: (key: string, value: unknown) => void;
}

function UserSelectorField({
  label,
  modeKey,
  targetKey,
  parameters,
  users,
  disabled,
  onChange,
  allowNone,
}: {
  label: string;
  modeKey: string;
  targetKey: string;
  parameters: Record<string, unknown>;
  users: UserSummary[];
  disabled: boolean;
  onChange: (key: string, value: unknown) => void;
  allowNone: boolean;
}) {
  const mode = (parameters[modeKey] as AutomationUserSelector) ?? 'TaskAssignee';
  const options: AutomationUserSelector[] = allowNone
    ? ['None', 'TaskAssignee', 'ProjectManager', 'SpecificUser']
    : ['TaskAssignee', 'ProjectManager', 'SpecificUser'];
  return (
    <>
      <label className="automation-builder__field-label">{label}</label>
      <div className="automation-builder__row">
        <select value={mode} onChange={(e) => onChange(modeKey, e.target.value)} disabled={disabled}>
          {options.map((o) => (
            <option key={o} value={o}>
              {USER_SELECTOR_LABELS[o]}
            </option>
          ))}
        </select>
        {mode === 'SpecificUser' && (
          <select value={(parameters[targetKey] as string) ?? ''} onChange={(e) => onChange(targetKey, e.target.value)} disabled={disabled}>
            <option value="">Select a person...</option>
            {users.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name}
              </option>
            ))}
          </select>
        )}
      </div>
    </>
  );
}

function TemplateHint() {
  return <p className="automation-builder__hint">Available variables: {TEMPLATE_VARIABLES.join(', ')}</p>;
}

function ActionParamsEditor({ actionType, parameters, users, disabled, onChange }: ActionParamsEditorProps) {
  switch (actionType) {
    case 'AssignTask':
      return (
        <UserSelectorField
          label="Assign to"
          modeKey="assignMode"
          targetKey="targetUserId"
          parameters={parameters}
          users={users}
          disabled={disabled}
          onChange={onChange}
          allowNone={false}
        />
      );
    case 'ChangeStatus':
      return (
        <select value={(parameters.newStatus as string) ?? 'InProgress'} onChange={(e) => onChange('newStatus', e.target.value)} disabled={disabled}>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {STATUS_LABELS[s]}
            </option>
          ))}
        </select>
      );
    case 'ChangePriority':
      return (
        <select value={(parameters.newPriority as string) ?? 'High'} onChange={(e) => onChange('newPriority', e.target.value)} disabled={disabled}>
          {PRIORITY_OPTIONS.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      );
    case 'AddLabel':
    case 'RemoveLabel':
    case 'AddFileTag':
      return (
        <input
          type="text"
          placeholder="Label name"
          value={(parameters.tagName as string) ?? ''}
          onChange={(e) => onChange('tagName', e.target.value)}
          disabled={disabled}
        />
      );
    case 'SetDueDate':
    case 'SetStartDate':
      return (
        <div className="automation-builder__row">
          <label className="automation-builder__inline-checkbox">
            <input
              type="checkbox"
              checked={(parameters.clearDate as boolean) ?? false}
              onChange={(e) => onChange('clearDate', e.target.checked)}
              disabled={disabled}
            />
            Clear date
          </label>
          {!parameters.clearDate && (
            <>
              <span>Days from now:</span>
              <input
                type="number"
                value={(parameters.offsetDays as number) ?? 0}
                onChange={(e) => onChange('offsetDays', Number(e.target.value) || 0)}
                disabled={disabled}
              />
            </>
          )}
        </div>
      );
    case 'AddComment':
      return (
        <>
          <textarea
            placeholder="This task is now high priority — {{task.title}}"
            value={(parameters.commentTemplate as string) ?? ''}
            onChange={(e) => onChange('commentTemplate', e.target.value)}
            rows={2}
            disabled={disabled}
          />
          <TemplateHint />
        </>
      );
    case 'SendNotification':
      return (
        <>
          <UserSelectorField
            label="Notify"
            modeKey="recipientMode"
            targetKey="targetUserId"
            parameters={parameters}
            users={users}
            disabled={disabled}
            onChange={onChange}
            allowNone={true}
          />
          <textarea
            placeholder="{{task.title}} needs your attention"
            value={(parameters.messageTemplate as string) ?? ''}
            onChange={(e) => onChange('messageTemplate', e.target.value)}
            rows={2}
            disabled={disabled}
          />
          <TemplateHint />
        </>
      );
    case 'CreateTask':
      return (
        <>
          <input
            type="text"
            placeholder="Title, e.g. Review: {{task.title}}"
            value={(parameters.titleTemplate as string) ?? ''}
            onChange={(e) => onChange('titleTemplate', e.target.value)}
            disabled={disabled}
          />
          <textarea
            placeholder="Description (optional)"
            value={(parameters.descriptionTemplate as string) ?? ''}
            onChange={(e) => onChange('descriptionTemplate', e.target.value)}
            rows={2}
            disabled={disabled}
          />
          <TemplateHint />
          <UserSelectorField
            label="Assign to"
            modeKey="assignMode"
            targetKey="targetUserId"
            parameters={parameters}
            users={users}
            disabled={disabled}
            onChange={onChange}
            allowNone={true}
          />
          <div className="automation-builder__row">
            <select value={(parameters.status as string) ?? 'NotStarted'} onChange={(e) => onChange('status', e.target.value)} disabled={disabled}>
              {STATUS_OPTIONS.map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABELS[s]}
                </option>
              ))}
            </select>
            <select value={(parameters.priority as string) ?? 'Medium'} onChange={(e) => onChange('priority', e.target.value)} disabled={disabled}>
              {PRIORITY_OPTIONS.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
            <span>Due in (days):</span>
            <input
              type="number"
              value={(parameters.dueDateOffsetDays as number) ?? ''}
              onChange={(e) => onChange('dueDateOffsetDays', e.target.value === '' ? null : Number(e.target.value))}
              disabled={disabled}
            />
          </div>
        </>
      );
  }
}
