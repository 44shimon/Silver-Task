import { useState, type FormEvent } from 'react';
import { ArrowDown, ArrowUp, X } from 'lucide-react';
import { Modal } from '@/components/shared/Modal';
import { ConfirmDeleteDialog } from '@/components/shared/ConfirmDeleteDialog';
import {
  CONDITION_OPERATOR_LABELS,
  CONDITION_OPERATOR_OPTIONS,
  CUSTOM_FIELD_TYPE_LABELS,
  CUSTOM_FIELD_TYPE_OPTIONS,
  customFieldTypeHasOptions,
  customFieldTypeSupportsMaxLength,
  customFieldTypeSupportsRange,
  type CustomField,
  type CustomFieldConditionOperator,
  type CustomFieldEntityType,
  type CustomFieldType,
} from '@/types/customField';
import type { Project } from '@/types/project';
import {
  useAdminAddCustomFieldOption,
  useAdminCreateCustomField,
  useAdminCustomFields,
  useAdminDeleteCustomFieldOption,
  useAdminUpdateCustomField,
  useAdminUpdateCustomFieldOption,
} from '@/hooks/useAdminCustomFields';
import { ApiError } from '@/api/httpClient';
import '@/pages/settings/SettingsForm.css';
import './CustomFieldFormModal.css';

interface CustomFieldFormModalProps {
  mode: 'create' | 'edit';
  field?: CustomField;
  projects: Project[];
  onClose: () => void;
}

const PROJECT_SCOPE_ALL = 'all';
const SYSTEM_ROLES = ['Administrator', 'Manager', 'Member', 'Viewer'];

// One modal for both create and edit: FieldType, EntityType, and project scope are only
// choosable at create time (changing any of them after values might exist could make those
// values impossible to interpret, or silently move the field's scope), everything else stays
// editable — same immutable-FieldType rule the existing project-scoped panel already follows.
export function CustomFieldFormModal({ mode, field, projects, onClose }: CustomFieldFormModalProps) {
  const createField = useAdminCreateCustomField();
  const updateField = useAdminUpdateCustomField();

  const [name, setName] = useState(field?.name ?? '');
  const [description, setDescription] = useState(field?.description ?? '');
  const [fieldType, setFieldType] = useState<CustomFieldType>(field?.fieldType ?? 'Text');
  const [entityType, setEntityType] = useState<CustomFieldEntityType>(field?.entityType ?? 'Task');
  const [isRequired, setIsRequired] = useState(field?.isRequired ?? false);
  const [isActive, setIsActive] = useState(field?.isActive ?? true);
  const [defaultValue, setDefaultValue] = useState(field?.defaultValue ?? '');
  const [scope, setScope] = useState<string>(field ? (field.projectId ?? PROJECT_SCOPE_ALL) : PROJECT_SCOPE_ALL);
  const [newOptions, setNewOptions] = useState<string[]>([]);
  const [newOptionDraft, setNewOptionDraft] = useState('');
  const [groupName, setGroupName] = useState(field?.groupName ?? '');
  const [placeholder, setPlaceholder] = useState(field?.placeholder ?? '');
  const [maxLength, setMaxLength] = useState(field?.maxLength?.toString() ?? '');
  const [minValue, setMinValue] = useState(field?.minValue?.toString() ?? '');
  const [maxValue, setMaxValue] = useState(field?.maxValue?.toString() ?? '');
  const [decimalPlaces, setDecimalPlaces] = useState(field?.decimalPlaces?.toString() ?? '');
  const [isPrivate, setIsPrivate] = useState(field?.isPrivate ?? false);
  const [visibleToRoles, setVisibleToRoles] = useState<string[]>(field?.visibleToRoles ? field.visibleToRoles.split(',') : []);
  const [conditionFieldId, setConditionFieldId] = useState(field?.conditionFieldId ?? '');
  const [conditionOperator, setConditionOperator] = useState<CustomFieldConditionOperator | ''>(field?.conditionOperator ?? '');
  const [conditionValue, setConditionValue] = useState(field?.conditionValue ?? '');
  const [formError, setFormError] = useState<string | null>(null);

  const mutation = mode === 'create' ? createField : updateField;

  // Candidate fields for the condition picker: same scope this field would live in, excluding
  // itself. Fetched reactively as the admin changes EntityType/scope in the create form.
  const scopeProjectId = scope === PROJECT_SCOPE_ALL ? undefined : scope;
  const { data: scopeFields } = useAdminCustomFields({ entityType, projectId: scopeProjectId });
  const conditionCandidates = (scopeFields ?? []).filter((f) => f.id !== field?.id);
  const conditionField = conditionCandidates.find((f) => f.id === conditionFieldId);

  function addOptionDraft() {
    const trimmed = newOptionDraft.trim();
    if (!trimmed) {
      return;
    }
    setNewOptions((prev) => [...prev, trimmed]);
    setNewOptionDraft('');
  }

  function toggleRole(role: string) {
    setVisibleToRoles((prev) => (prev.includes(role) ? prev.filter((r) => r !== role) : [...prev, role]));
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      return;
    }
    setFormError(null);

    const shared = {
      groupName: groupName.trim() || undefined,
      placeholder: customFieldTypeSupportsMaxLength(fieldType) ? placeholder.trim() || undefined : undefined,
      maxLength: customFieldTypeSupportsMaxLength(fieldType) && maxLength ? Number(maxLength) : undefined,
      minValue: customFieldTypeSupportsRange(fieldType) && minValue ? Number(minValue) : undefined,
      maxValue: customFieldTypeSupportsRange(fieldType) && maxValue ? Number(maxValue) : undefined,
      decimalPlaces: customFieldTypeSupportsRange(fieldType) && decimalPlaces ? Number(decimalPlaces) : undefined,
      isPrivate,
      visibleToRoles: visibleToRoles.length > 0 ? visibleToRoles.join(',') : undefined,
      conditionFieldId: conditionFieldId || undefined,
      conditionOperator: conditionFieldId ? (conditionOperator || undefined) : undefined,
      conditionValue: conditionFieldId ? (conditionValue.trim() || undefined) : undefined,
    };

    if (mode === 'create') {
      createField.mutate(
        {
          name: trimmedName,
          description: description.trim() || undefined,
          fieldType,
          entityType,
          isRequired,
          defaultValue: defaultValue.trim() || undefined,
          options: customFieldTypeHasOptions(fieldType) ? newOptions : undefined,
          projectId: scope === PROJECT_SCOPE_ALL ? null : scope,
          ...shared,
        },
        {
          onSuccess: onClose,
          onError: (error) => setFormError(error instanceof ApiError ? error.message : 'Could not create field.'),
        },
      );
    } else if (field) {
      updateField.mutate(
        {
          id: field.id,
          request: {
            name: trimmedName,
            description: description.trim() || null,
            isRequired,
            isActive,
            defaultValue: defaultValue.trim() || null,
            sortOrder: field.sortOrder,
            ...shared,
          },
        },
        {
          onSuccess: onClose,
          onError: (error) => setFormError(error instanceof ApiError ? error.message : 'Could not save field.'),
        },
      );
    }
  }

  return (
    <Modal onClose={onClose} size="wide">
      <h2>{mode === 'create' ? 'New Custom Field' : `Edit "${field?.name}"`}</h2>
      <form className="settings-form custom-field-form" onSubmit={handleSubmit}>
        <div className="settings-form__field">
          <label htmlFor="cf-name">Name</label>
          <input id="cf-name" type="text" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Contractor" />
        </div>

        <div className="settings-form__field">
          <label htmlFor="cf-description">Description</label>
          <input
            id="cf-description"
            type="text"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Optional — shown as help text to users"
          />
        </div>

        {mode === 'create' ? (
          <div className="settings-form__row">
            <div className="settings-form__field">
              <label htmlFor="cf-type">Type</label>
              <select id="cf-type" value={fieldType} onChange={(e) => setFieldType(e.target.value as CustomFieldType)}>
                {CUSTOM_FIELD_TYPE_OPTIONS.map((type) => (
                  <option key={type} value={type}>
                    {CUSTOM_FIELD_TYPE_LABELS[type]}
                  </option>
                ))}
              </select>
            </div>
            <div className="settings-form__field">
              <label htmlFor="cf-entity-type">Applies to</label>
              <select id="cf-entity-type" value={entityType} onChange={(e) => setEntityType(e.target.value as CustomFieldEntityType)}>
                <option value="Task">Tasks</option>
                <option value="Project">Projects</option>
              </select>
            </div>
          </div>
        ) : (
          <div className="settings-form__row">
            <div className="settings-form__readonly">
              <span className="settings-form__readonly-label">Type</span>
              <span className="settings-form__readonly-value">{CUSTOM_FIELD_TYPE_LABELS[field!.fieldType]}</span>
            </div>
            <div className="settings-form__readonly">
              <span className="settings-form__readonly-label">Applies to</span>
              <span className="settings-form__readonly-value">{field!.entityType === 'Project' ? 'Projects' : 'Tasks'}</span>
            </div>
          </div>
        )}

        {mode === 'create' ? (
          <div className="settings-form__field">
            <label htmlFor="cf-scope">Project scope</label>
            <select id="cf-scope" value={scope} onChange={(e) => setScope(e.target.value)}>
              <option value={PROJECT_SCOPE_ALL}>All Projects</option>
              {projects.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.name}
                </option>
              ))}
            </select>
          </div>
        ) : (
          <div className="settings-form__readonly">
            <span className="settings-form__readonly-label">Project scope</span>
            <span className="settings-form__readonly-value">{field!.projectName ?? 'All Projects'}</span>
          </div>
        )}

        <div className="settings-form__field">
          <label htmlFor="cf-group">Group</label>
          <input
            id="cf-group"
            type="text"
            value={groupName}
            onChange={(e) => setGroupName(e.target.value)}
            placeholder="Optional — e.g. Property Information"
          />
        </div>

        {customFieldTypeSupportsMaxLength(fieldType) && (
          <div className="settings-form__row">
            <div className="settings-form__field">
              <label htmlFor="cf-placeholder">Placeholder</label>
              <input id="cf-placeholder" type="text" value={placeholder} onChange={(e) => setPlaceholder(e.target.value)} />
            </div>
            <div className="settings-form__field">
              <label htmlFor="cf-maxlength">Max Length</label>
              <input id="cf-maxlength" type="number" min={1} value={maxLength} onChange={(e) => setMaxLength(e.target.value)} />
            </div>
          </div>
        )}

        {customFieldTypeSupportsRange(fieldType) && (
          <div className="settings-form__row">
            <div className="settings-form__field">
              <label htmlFor="cf-min">Minimum</label>
              <input id="cf-min" type="number" value={minValue} onChange={(e) => setMinValue(e.target.value)} />
            </div>
            <div className="settings-form__field">
              <label htmlFor="cf-max">Maximum</label>
              <input id="cf-max" type="number" value={maxValue} onChange={(e) => setMaxValue(e.target.value)} />
            </div>
            <div className="settings-form__field">
              <label htmlFor="cf-decimals">Decimal Places</label>
              <input id="cf-decimals" type="number" min={0} value={decimalPlaces} onChange={(e) => setDecimalPlaces(e.target.value)} placeholder="0 = whole numbers" />
            </div>
          </div>
        )}

        <div className="settings-form__field">
          <label htmlFor="cf-default">Default value</label>
          <input
            id="cf-default"
            type="text"
            value={defaultValue}
            onChange={(e) => setDefaultValue(e.target.value)}
            placeholder="Optional"
            disabled={customFieldTypeHasOptions(fieldType) || fieldType === 'User' || fieldType === 'UserMulti'}
          />
        </div>

        <div className="settings-toggle-row">
          <div className="settings-toggle-row__label">
            <span className="settings-toggle-row__title">Required</span>
            <span className="settings-toggle-row__description">Once set, this field's value can't be cleared back to empty.</span>
          </div>
          <button
            type="button"
            className={`settings-toggle${isRequired ? ' settings-toggle--on' : ''}`}
            role="switch"
            aria-checked={isRequired}
            aria-label="Required"
            onClick={() => setIsRequired((prev) => !prev)}
          />
        </div>

        {mode === 'edit' && (
          <div className="settings-toggle-row">
            <div className="settings-toggle-row__label">
              <span className="settings-toggle-row__title">Active</span>
              <span className="settings-toggle-row__description">
                Disabled fields keep their existing values but can't be given a new one.
              </span>
            </div>
            <button
              type="button"
              className={`settings-toggle${isActive ? ' settings-toggle--on' : ''}`}
              role="switch"
              aria-checked={isActive}
              aria-label="Active"
              onClick={() => setIsActive((prev) => !prev)}
            />
          </div>
        )}

        <div className="settings-toggle-row">
          <div className="settings-toggle-row__label">
            <span className="settings-toggle-row__title">Private</span>
            <span className="settings-toggle-row__description">
              Only an Administrator, the project owner/Manager, or a role listed below can see this field's value.
            </span>
          </div>
          <button
            type="button"
            className={`settings-toggle${isPrivate ? ' settings-toggle--on' : ''}`}
            role="switch"
            aria-checked={isPrivate}
            aria-label="Private"
            onClick={() => setIsPrivate((prev) => !prev)}
          />
        </div>

        {isPrivate && (
          <div className="custom-field-form__roles">
            <span>Also visible to:</span>
            {SYSTEM_ROLES.map((role) => (
              <label key={role} className="custom-field-form__role-checkbox">
                <input type="checkbox" checked={visibleToRoles.includes(role)} onChange={() => toggleRole(role)} />
                {role}
              </label>
            ))}
          </div>
        )}

        <div className="custom-field-form__condition">
          <span className="custom-field-form__condition-title">Show this field when: (optional)</span>
          <div className="custom-field-form__condition-row">
            <select value={conditionFieldId} onChange={(e) => setConditionFieldId(e.target.value)}>
              <option value="">Always visible</option>
              {conditionCandidates.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </select>
            {conditionFieldId && (
              <>
                <select value={conditionOperator} onChange={(e) => setConditionOperator(e.target.value as CustomFieldConditionOperator)}>
                  <option value="">Select an operator...</option>
                  {CONDITION_OPERATOR_OPTIONS.map((op) => (
                    <option key={op} value={op}>
                      {CONDITION_OPERATOR_LABELS[op]}
                    </option>
                  ))}
                </select>
                {conditionOperator !== 'IsEmpty' && conditionOperator !== 'IsNotEmpty' && (
                  conditionField && customFieldTypeHasOptions(conditionField.fieldType) ? (
                    <select value={conditionValue} onChange={(e) => setConditionValue(e.target.value)}>
                      <option value="">Select a value...</option>
                      {conditionField.options.map((o) => (
                        <option key={o.id} value={o.id}>
                          {o.value}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input type="text" value={conditionValue} onChange={(e) => setConditionValue(e.target.value)} placeholder="Value" />
                  )
                )}
              </>
            )}
          </div>
        </div>

        {customFieldTypeHasOptions(fieldType) && mode === 'create' && (
          <div className="custom-field-form__options-builder">
            <span>Initial options</span>
            <div className="custom-field-form__option-tags">
              {newOptions.map((option, index) => (
                <span key={`${option}-${index}`} className="option-tag">
                  {option}
                  <button type="button" onClick={() => setNewOptions((prev) => prev.filter((_, i) => i !== index))} aria-label={`Remove ${option}`}>
                    <X size={11} />
                  </button>
                </span>
              ))}
            </div>
            <div className="custom-field-form__option-input-row">
              <input
                type="text"
                value={newOptionDraft}
                onChange={(e) => setNewOptionDraft(e.target.value)}
                placeholder="Add option"
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    addOptionDraft();
                  }
                }}
              />
              <button type="button" onClick={addOptionDraft}>
                Add
              </button>
            </div>
          </div>
        )}

        {mode === 'edit' && field && customFieldTypeHasOptions(field.fieldType) && <ExistingOptionsEditor field={field} />}

        {formError && <p className="form-error">{formError}</p>}

        <div className="custom-field-form__actions">
          <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="settings-form__save" disabled={mutation.isPending || !name.trim()}>
            {mutation.isPending ? 'Saving...' : mode === 'create' ? 'Create Field' : 'Save Changes'}
          </button>
        </div>
      </form>
    </Modal>
  );
}

function ExistingOptionsEditor({ field }: { field: CustomField }) {
  const addOption = useAdminAddCustomFieldOption();
  const updateOption = useAdminUpdateCustomFieldOption();
  const deleteOption = useAdminDeleteCustomFieldOption();
  const [draft, setDraft] = useState('');
  const [conflict, setConflict] = useState<{ optionId: string; value: string; message: string } | null>(null);

  const sortedOptions = [...field.options].sort((a, b) => a.sortOrder - b.sortOrder);

  function move(index: number, direction: -1 | 1) {
    const other = sortedOptions[index + direction];
    const current = sortedOptions[index];
    if (!other) {
      return;
    }
    updateOption.mutate({ fieldId: field.id, optionId: current.id, request: { value: current.value, sortOrder: other.sortOrder } });
    updateOption.mutate({ fieldId: field.id, optionId: other.id, request: { value: other.value, sortOrder: current.sortOrder } });
  }

  function handleDelete(optionId: string, value: string) {
    deleteOption.mutate(
      { fieldId: field.id, optionId },
      {
        onError: (error) => {
          if (error instanceof ApiError && error.status === 409) {
            setConflict({ optionId, value, message: error.message });
          }
        },
      },
    );
  }

  return (
    <div className="custom-field-form__options-editor">
      <span>Options</span>
      {sortedOptions.map((option, index) => (
        <div key={option.id} className={`custom-field-form__option-row${option.isActive ? '' : ' custom-field-form__option-row--inactive'}`}>
          <input
            type="text"
            value={option.value}
            onChange={(e) =>
              updateOption.mutate({ fieldId: field.id, optionId: option.id, request: { value: e.target.value } })
            }
          />
          <button type="button" aria-label="Move up" disabled={index === 0} onClick={() => move(index, -1)}>
            <ArrowUp size={12} />
          </button>
          <button type="button" aria-label="Move down" disabled={index === sortedOptions.length - 1} onClick={() => move(index, 1)}>
            <ArrowDown size={12} />
          </button>
          <button
            type="button"
            className="custom-field-form__option-toggle"
            onClick={() =>
              updateOption.mutate({ fieldId: field.id, optionId: option.id, request: { value: option.value, isActive: !option.isActive } })
            }
          >
            {option.isActive ? 'Disable' : 'Enable'}
          </button>
          <button type="button" aria-label={`Delete ${option.value}`} onClick={() => handleDelete(option.id, option.value)}>
            <X size={12} />
          </button>
        </div>
      ))}

      <div className="custom-field-form__option-input-row">
        <input
          type="text"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="Add option"
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              const trimmed = draft.trim();
              if (trimmed) {
                addOption.mutate({ fieldId: field.id, value: trimmed }, { onSuccess: () => setDraft('') });
              }
            }
          }}
        />
        <button
          type="button"
          onClick={() => {
            const trimmed = draft.trim();
            if (trimmed) {
              addOption.mutate({ fieldId: field.id, value: trimmed }, { onSuccess: () => setDraft('') });
            }
          }}
        >
          Add
        </button>
      </div>

      {conflict && (
        <ConfirmDeleteDialog
          title={`Delete "${conflict.value}"?`}
          message={conflict.message}
          isDeleting={deleteOption.isPending}
          onClose={() => setConflict(null)}
          onConfirmDelete={() =>
            deleteOption.mutate(
              { fieldId: field.id, optionId: conflict.optionId, confirm: true },
              { onSuccess: () => setConflict(null) },
            )
          }
        />
      )}
    </div>
  );
}
