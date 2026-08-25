import { useCallback, useMemo, useState } from 'react';
import { flexRender } from '@tanstack/react-table';
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { Pencil, Trash2 } from 'lucide-react';
import type { CustomField, CustomFieldType } from '@/types/customField';
import { CUSTOM_FIELD_TYPE_LABELS } from '@/types/customField';
import { useAdminDeleteCustomField } from '@/hooks/useAdminCustomFields';
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
  const [conflict, setConflict] = useState<{ id: string; name: string; message: string } | null>(null);

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

  const columns = useMemo<LegacyColumnDef<CustomField, any>[]>(
    () => [
      columnHelper.accessor('name', {
        header: 'Name',
        size: 180,
        minSize: 120,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue()}</span>,
      }),
      columnHelper.accessor('fieldType', {
        header: 'Type',
        size: 110,
        minSize: 90,
        cell: (info) => (
          <span className="admin-table__readonly-text">{CUSTOM_FIELD_TYPE_LABELS[info.getValue() as CustomFieldType]}</span>
        ),
      }),
      columnHelper.accessor('projectName', {
        header: 'Project',
        size: 160,
        minSize: 120,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue() ?? 'All Projects'}</span>,
      }),
      columnHelper.accessor('isRequired', {
        header: 'Required',
        size: 90,
        minSize: 80,
        cell: (info) => (
          <span className={`admin-custom-field-badge${info.getValue() ? ' admin-custom-field-badge--on' : ''}`}>
            {info.getValue() ? 'Yes' : 'No'}
          </span>
        ),
      }),
      columnHelper.accessor('isActive', {
        header: 'Active',
        size: 90,
        minSize: 80,
        cell: (info) => (
          <span className={`admin-project-status admin-project-status--${info.getValue() ? 'active' : 'archived'}`}>
            {info.getValue() ? 'Active' : 'Inactive'}
          </span>
        ),
      }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        size: 110,
        minSize: 100,
        cell: (info) => <span className="admin-table__readonly-text">{formatDateTime(info.getValue())}</span>,
      }),
      columnHelper.accessor('updatedAt', {
        header: 'Updated',
        size: 110,
        minSize: 100,
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
    [deleteField.isPending, handleDelete, onEdit],
  );

  const table = useLegacyTable({
    data: fields,
    columns,
    columnResizeMode: 'onChange',
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="task-table-wrapper">
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
