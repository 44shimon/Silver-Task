import { useState, type FormEvent } from 'react';
import { Settings, Trash2, X } from 'lucide-react';
import {
  CUSTOM_FIELD_TYPE_LABELS,
  CUSTOM_FIELD_TYPE_OPTIONS,
  customFieldTypeHasOptions,
  type CustomField,
  type CustomFieldType,
} from '@/types/customField';
import {
  useAddCustomFieldOption,
  useCreateCustomField,
  useCustomFields,
  useDeleteCustomField,
  useDeleteCustomFieldOption,
} from '@/hooks/useCustomFields';
import { ApiError } from '@/api/httpClient';
import { ConfirmDeleteDialog } from '@/components/shared/ConfirmDeleteDialog';
import './Toolbar.css';
import './CustomFieldsPanel.css';

interface CustomFieldsPanelProps {
  projectId: string;
}

export function CustomFieldsPanel({ projectId }: CustomFieldsPanelProps) {
  const { data: fields } = useCustomFields(projectId);
  const createField = useCreateCustomField(projectId);
  const deleteField = useDeleteCustomField(projectId);

  const [newName, setNewName] = useState('');
  const [newType, setNewType] = useState<CustomFieldType>('Text');
  const [newOptions, setNewOptions] = useState<string[]>([]);
  const [newOptionDraft, setNewOptionDraft] = useState('');
  const [fieldConflict, setFieldConflict] = useState<{ id: string; name: string; message: string } | null>(null);

  function handleDeleteField(field: CustomField) {
    deleteField.mutate(
      { id: field.id },
      {
        onError: (error) => {
          if (error instanceof ApiError && error.status === 409) {
            setFieldConflict({ id: field.id, name: field.name, message: error.message });
          }
        },
      },
    );
  }

  function addOptionDraft() {
    const trimmed = newOptionDraft.trim();
    if (!trimmed) {
      return;
    }
    setNewOptions((prev) => [...prev, trimmed]);
    setNewOptionDraft('');
  }

  function removeOptionDraft(index: number) {
    setNewOptions((prev) => prev.filter((_, i) => i !== index));
  }

  function handleCreateField(event: FormEvent) {
    event.preventDefault();
    const trimmedName = newName.trim();
    if (!trimmedName) {
      return;
    }

    createField.mutate(
      {
        name: trimmedName,
        fieldType: newType,
        options: customFieldTypeHasOptions(newType) ? newOptions : undefined,
      },
      {
        onSuccess: () => {
          setNewName('');
          setNewType('Text');
          setNewOptions([]);
        },
      },
    );
  }

  return (
    <details className="toolbar-popover">
      <summary className="toolbar-button">
        <Settings size={14} />
        <span>Custom Fields{fields && fields.length > 0 ? ` (${fields.length})` : ''}</span>
      </summary>
      <div className="toolbar-popover__panel custom-fields-panel">
        <div className="custom-fields-panel__list">
          {fields?.map((field) => (
            <CustomFieldRow
              key={field.id}
              projectId={projectId}
              field={field}
              onDelete={() => handleDeleteField(field)}
            />
          ))}
          {fields?.length === 0 && <p className="custom-fields-panel__empty">No custom fields yet.</p>}
        </div>

        <form className="custom-fields-panel__form" onSubmit={handleCreateField}>
          <label className="toolbar-popover__field">
            <span>Name</span>
            <input
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="e.g. Contractor"
            />
          </label>

          <label className="toolbar-popover__field">
            <span>Type</span>
            <select value={newType} onChange={(e) => setNewType(e.target.value as CustomFieldType)}>
              {CUSTOM_FIELD_TYPE_OPTIONS.map((type) => (
                <option key={type} value={type}>
                  {CUSTOM_FIELD_TYPE_LABELS[type]}
                </option>
              ))}
            </select>
          </label>

          {customFieldTypeHasOptions(newType) && (
            <div className="custom-fields-panel__options-builder">
              <span>Options</span>
              <div className="custom-fields-panel__option-tags">
                {newOptions.map((option, index) => (
                  <span key={`${option}-${index}`} className="option-tag">
                    {option}
                    <button type="button" onClick={() => removeOptionDraft(index)} aria-label={`Remove ${option}`}>
                      <X size={11} />
                    </button>
                  </span>
                ))}
              </div>
              <div className="custom-fields-panel__option-input-row">
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

          <button type="submit" className="custom-fields-panel__submit" disabled={createField.isPending || !newName.trim()}>
            + Add Field
          </button>
          {createField.isError && (
            <p className="form-error">
              {createField.error instanceof ApiError ? createField.error.message : 'Could not create field.'}
            </p>
          )}
        </form>
      </div>

      {fieldConflict && (
        <ConfirmDeleteDialog
          title={`Delete "${fieldConflict.name}"?`}
          message={fieldConflict.message}
          isDeleting={deleteField.isPending}
          onClose={() => setFieldConflict(null)}
          onConfirmDelete={() =>
            deleteField.mutate({ id: fieldConflict.id, confirm: true }, { onSuccess: () => setFieldConflict(null) })
          }
        />
      )}
    </details>
  );
}

interface CustomFieldRowProps {
  projectId: string;
  field: CustomField;
  onDelete: () => void;
}

function CustomFieldRow({ projectId, field, onDelete }: CustomFieldRowProps) {
  const addOption = useAddCustomFieldOption(projectId);
  const deleteOption = useDeleteCustomFieldOption(projectId);
  const [optionDraft, setOptionDraft] = useState('');
  const [optionConflict, setOptionConflict] = useState<{ optionId: string; value: string; message: string } | null>(null);

  function handleAddOption() {
    const trimmed = optionDraft.trim();
    if (!trimmed) {
      return;
    }
    addOption.mutate({ fieldId: field.id, value: trimmed }, { onSuccess: () => setOptionDraft('') });
  }

  function handleDeleteOption(optionId: string, value: string) {
    deleteOption.mutate(
      { fieldId: field.id, optionId },
      {
        onError: (error) => {
          if (error instanceof ApiError && error.status === 409) {
            setOptionConflict({ optionId, value, message: error.message });
          }
        },
      },
    );
  }

  return (
    <div className="custom-field-row">
      <div className="custom-field-row__header">
        <span className="custom-field-row__name">{field.name}</span>
        <span className="custom-field-row__type">{CUSTOM_FIELD_TYPE_LABELS[field.fieldType]}</span>
        <button type="button" className="icon-button" aria-label={`Delete ${field.name}`} onClick={onDelete}>
          <Trash2 size={13} />
        </button>
      </div>

      {customFieldTypeHasOptions(field.fieldType) && (
        <div className="custom-field-row__options">
          {field.options.map((option) => (
            <span key={option.id} className="option-tag">
              {option.value}
              <button
                type="button"
                aria-label={`Remove ${option.value}`}
                onClick={() => handleDeleteOption(option.id, option.value)}
              >
                <X size={11} />
              </button>
            </span>
          ))}
          <div className="custom-field-row__option-input-row">
            <input
              type="text"
              value={optionDraft}
              onChange={(e) => setOptionDraft(e.target.value)}
              placeholder="Add option"
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  handleAddOption();
                }
              }}
            />
            <button type="button" onClick={handleAddOption}>
              Add
            </button>
          </div>
        </div>
      )}

      {optionConflict && (
        <ConfirmDeleteDialog
          title={`Delete "${optionConflict.value}"?`}
          message={optionConflict.message}
          isDeleting={deleteOption.isPending}
          onClose={() => setOptionConflict(null)}
          onConfirmDelete={() =>
            deleteOption.mutate(
              { fieldId: field.id, optionId: optionConflict.optionId, confirm: true },
              { onSuccess: () => setOptionConflict(null) },
            )
          }
        />
      )}
    </div>
  );
}
