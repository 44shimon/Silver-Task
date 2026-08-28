import { useCallback, useMemo, useState } from 'react';
import { flexRender } from '@tanstack/react-table';
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { GripVertical, Pencil, Trash2 } from 'lucide-react';
import type { CustomField, CustomFieldType } from '@/types/customField';
import { CUSTOM_FIELD_TYPE_LABELS } from '@/types/customField';
import { useAdminDeleteCustomField, useAdminReorderCustomFields } from '@/hooks/useAdminCustomFields';
import { ApiError } from '@/api/httpClient';
import { ConfirmDeleteDialog } from '@/components/shared/ConfirmDeleteDialog';
import { formatDateTime } from '@/utils/formatDate';
import '@/components/spreadsheet/TaskTable.css';
import '@/components/admin/AdminProjectsTable.css';
import './AdminCustomFieldsTable.css';

interface AdminCustomFieldsTableProps {
  fields: CustomField[];
  onEdit: (field: CustomField) => void;
}

const columnHelper = legacyCreateColumnHelper<CustomField>();

export function AdminCustomFieldsTable({ fields, onEdit }: AdminCustomFieldsTableProps) {
  const deleteField = useAdminDeleteCustomField();
  const reorder = useAdminReorderCustomFields();
  const [conflict, setConflict] = useState<{ id: string; name: string; message: string } | null>(null);
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [dragId, setDragId] = useState<string | null>(null);

  const handleDelete = useCallback(
    (field: CustomField) => {
      deleteField.mutate(
        { id: field.id },
        {
          onError: (error) => {
            if (error instanceof ApiError && error.status === 409) {
              setConflict({ id: field.id, name: field.name, message: error.message });
            }
          },
        },
      );
    },
    [deleteField],
  );

  // Drag-and-drop reordering only makes sense within one scope (same EntityType + Project) —
  // the backend rejects mixing scopes in one reorder call. Rather than pre-computing which rows
  // are draggable, this just lets the drop happen and surfaces the backend's own rejection
  // message if the currently-displayed list spans more than one scope (the admin table can show
  // an unfiltered, multi-scope list) — a disclosed, simple tradeoff over pre-validating client-side.
  function handleDrop(targetId: string) {
    if (!dragId || dragId === targetId) {
      setDragId(null);
      return;
    }
    const next = [...fields];
    const fromIndex = next.findIndex((f) => f.id === dragId);
    const toIndex = next.findIndex((f) => f.id === targetId);
    setDragId(null);
    if (fromIndex === -1 || toIndex === -1) return;

    const [moved] = next.splice(fromIndex, 1);
    next.splice(toIndex, 0, moved);
    setReorderError(null);
    reorder.mutate(
      next.map((f) => f.id),
      {
        onError: (error) =>
          setReorderError(
            error instanceof ApiError
              ? error.message
              : 'Could not reorder fields — filter to a single project and scope first.',
          ),
      },
    );
  }

  const columns = useMemo<LegacyColumnDef<CustomField, any>[]>(
    () => [
      columnHelper.display({
        id: 'drag',
        header: '',
        size: 32,
        minSize: 32,
        enableResizing: false,
        cell: (info) => (
          <span
            className="admin-custom-fields-table__drag-handle"
            draggable
            onDragStart={() => setDragId(info.row.original.id)}
            onDragOver={(e) => e.preventDefault()}
            onDrop={() => handleDrop(info.row.original.id)}
          >
            <GripVertical size={14} />
          </span>
        ),
      }),
      columnHelper.accessor('name', {
        header: 'Name',
        size: 170,
        minSize: 110,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue()}</span>,
      }),
      columnHelper.accessor('identifier', {
        header: 'Identifier',
        size: 150,
        minSize: 100,
        cell: (info) => <code className="admin-custom-fields-table__identifier">{info.getValue()}</code>,
      }),
      columnHelper.accessor('fieldType', {
        header: 'Type',
        size: 100,
        minSize: 90,
        cell: (info) => (
          <span className="admin-table__readonly-text">{CUSTOM_FIELD_TYPE_LABELS[info.getValue() as CustomFieldType]}</span>
        ),
      }),
      columnHelper.accessor('entityType', {
        header: 'Scope',
        size: 80,
        minSize: 70,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue()}</span>,
      }),
      columnHelper.accessor('projectName', {
        header: 'Project',
        size: 150,
        minSize: 110,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue() ?? 'All Projects'}</span>,
      }),
      columnHelper.accessor('isRequired', {
        header: 'Required',
        size: 85,
        minSize: 75,
        cell: (info) => (
          <span className={`admin-custom-field-badge${info.getValue() ? ' admin-custom-field-badge--on' : ''}`}>
            {info.getValue() ? 'Yes' : 'No'}
          </span>
        ),
      }),
      columnHelper.accessor('isPrivate', {
        header: 'Private',
        size: 75,
        minSize: 70,
        cell: (info) => (
          <span className={`admin-custom-field-badge${info.getValue() ? ' admin-custom-field-badge--on' : ''}`}>
            {info.getValue() ? 'Yes' : 'No'}
          </span>
        ),
      }),
      columnHelper.accessor('isActive', {
        header: 'Active',
        size: 85,
        minSize: 75,
        cell: (info) => (
          <span className={`admin-project-status admin-project-status--${info.getValue() ? 'active' : 'archived'}`}>
            {info.getValue() ? 'Active' : 'Inactive'}
          </span>
        ),
      }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        size: 105,
        minSize: 95,
        cell: (info) => <span className="admin-table__readonly-text">{formatDateTime(info.getValue())}</span>,
      }),
      columnHelper.display({
        id: 'actions',
        header: '',
        size: 90,
        minSize: 90,
        enableResizing: false,
        cell: (info) => {
          const field = info.row.original;
          return (
            <div className="task-table__actions">
              <button type="button" className="icon-button" aria-label={`Edit ${field.name}`} title="Edit" onClick={() => onEdit(field)}>
                <Pencil size={14} />
              </button>
              <button
                type="button"
                className="icon-button admin-projects-table__delete"
                aria-label={`Delete ${field.name}`}
                title="Delete"
                disabled={deleteField.isPending}
                onClick={() => handleDelete(field)}
              >
                <Trash2 size={14} />
              </button>
            </div>
          );
        },
      }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [deleteField.isPending, handleDelete, onEdit, dragId, fields],
  );

  const table = useLegacyTable({
    data: fields,
    columns,
    columnResizeMode: 'onChange',
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="task-table-wrapper">
      {reorderError && <p className="form-error">{reorderError}</p>}
      <table className="task-table" style={{ width: table.getTotalSize() }}>
        <thead>
          {table.getHeaderGroups().map((headerGroup) => (
            <tr key={headerGroup.id}>
              {headerGroup.headers.map((header) => (
                <th key={header.id} style={{ width: header.getSize() }}>
                  <div className="task-table__header-content">
                    {flexRender(header.column.columnDef.header, header.getContext())}
                  </div>
                  {header.column.getCanResize() && (
                    <div
                      className={`task-table__resizer${header.column.getIsResizing() ? ' task-table__resizer--active' : ''}`}
                      onMouseDown={header.getResizeHandler()}
                      onTouchStart={header.getResizeHandler()}
                    />
                  )}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr key={row.id}>
              {row.getVisibleCells().map((cell) => (
                <td key={cell.id} style={{ width: cell.column.getSize() }}>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </td>
              ))}
            </tr>
          ))}
          {fields.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="task-table__empty-state">
                No custom fields found.
              </td>
            </tr>
          )}
        </tbody>
      </table>

      {conflict && (
        <ConfirmDeleteDialog
          title={`Delete "${conflict.name}"?`}
          message={conflict.message}
          isDeleting={deleteField.isPending}
          onClose={() => setConflict(null)}
          onConfirmDelete={() =>
            deleteField.mutate({ id: conflict.id, confirm: true }, { onSuccess: () => setConflict(null) })
          }
        />
      )}
    </div>
  );
}
