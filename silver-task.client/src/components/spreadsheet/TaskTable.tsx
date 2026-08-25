import { useMemo } from 'react';
import { flexRender } from '@tanstack/react-table';
// TanStack Table v9 replaced useReactTable with a new modular `features`-based
// useTable API. useLegacyTable is the officially bundled v8-compatible layer —
// used here deliberately (not left over from a migration) since a static grid
// with resizable columns doesn't need v9's tree-shakeable feature system, and
// the classic API is far less code to get right.
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { Copy, Maximize2, Trash2 } from 'lucide-react';
import type { Task } from '@/types/task';
import type { UserSummary } from '@/types/project';
import type { CustomField } from '@/types/customField';
import type { TaskSortField } from '@/hooks/useTaskFilters';
import type { SortDirection } from '@/utils/taskFilters';
import { EditableTitleCell } from './EditableTitleCell';
import { EditableDateCell } from './EditableDateCell';
import { StatusDropdownCell } from './StatusDropdownCell';
import { PriorityDropdownCell } from './PriorityDropdownCell';
import { AssignedToDropdownCell } from './AssignedToDropdownCell';
import { SortableColumnHeader } from './SortableColumnHeader';
import { CustomFieldCell } from './CustomFieldCell';
import { DependencySummaryCell } from './DependencySummaryCell';
import './TaskTable.css';

interface TaskTableProps {
  projectId: string;
  tasks: Task[];
  members: UserSummary[];
  customFields: CustomField[];
  isFiltered: boolean;
  sortField: TaskSortField;
  sortDirection: SortDirection;
  onSortFieldClick: (field: TaskSortField) => void;
  onDuplicate: (taskId: string) => void;
  onDelete: (taskId: string) => void;
  onOpenDetail: (taskId: string) => void;
}

const columnHelper = legacyCreateColumnHelper<Task>();

export function TaskTable({
  projectId,
  tasks,
  members,
  customFields,
  isFiltered,
  sortField,
  sortDirection,
  onSortFieldClick,
  onDuplicate,
  onDelete,
  onOpenDetail,
}: TaskTableProps) {
  const columns = useMemo<LegacyColumnDef<Task, any>[]>(
    () => [
      columnHelper.display({
        id: 'expand',
        header: '',
        size: 32,
        minSize: 32,
        enableResizing: false,
        cell: (info) => (
          <button
            type="button"
            className="icon-button"
            aria-label="Open task details"
            onClick={() => onOpenDetail(info.row.original.id)}
          >
            <Maximize2 size={12} />
          </button>
        ),
      }),
      columnHelper.accessor('title', {
        header: () => (
          <SortableColumnHeader
            label="Task"
            field="title"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 280,
        minSize: 160,
        cell: (info) => <EditableTitleCell task={info.row.original} projectId={projectId} />,
      }),
      columnHelper.accessor('status', {
        header: () => (
          <SortableColumnHeader
            label="Status"
            field="status"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 150,
        minSize: 130,
        cell: (info) => <StatusDropdownCell task={info.row.original} projectId={projectId} />,
      }),
      columnHelper.accessor('priority', {
        header: () => (
          <SortableColumnHeader
            label="Priority"
            field="priority"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 130,
        minSize: 110,
        cell: (info) => <PriorityDropdownCell task={info.row.original} projectId={projectId} />,
      }),
      columnHelper.accessor((task) => task.assignedTo?.name ?? '', {
        id: 'assignedTo',
        header: () => (
          <SortableColumnHeader
            label="Assigned To"
            field="assignedTo"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 170,
        minSize: 130,
        cell: (info) => (
          <AssignedToDropdownCell task={info.row.original} projectId={projectId} members={members} />
        ),
      }),
      columnHelper.accessor('startDate', {
        header: 'Start Date',
        size: 120,
        minSize: 100,
        cell: (info) => <EditableDateCell task={info.row.original} projectId={projectId} field="startDate" />,
      }),
      columnHelper.accessor('dueDate', {
        header: () => (
          <SortableColumnHeader
            label="Due Date"
            field="dueDate"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 120,
        minSize: 100,
        cell: (info) => <EditableDateCell task={info.row.original} projectId={projectId} field="dueDate" />,
      }),
      columnHelper.display({
        id: 'dependencies',
        header: 'Dependencies',
        size: 130,
        minSize: 110,
        cell: (info) => <DependencySummaryCell task={info.row.original} />,
      }),
      ...customFields.map((field) =>
        columnHelper.display({
          id: `custom-${field.id}`,
          header: field.name,
          size: 160,
          minSize: 120,
          cell: (info) => (
            <CustomFieldCell task={info.row.original} field={field} projectId={projectId} members={members} />
          ),
        }),
      ),
      columnHelper.display({
        id: 'actions',
        header: '',
        size: 76,
        minSize: 76,
        enableResizing: false,
        cell: (info) => (
          <div className="task-table__actions">
            <button
              type="button"
              className="icon-button"
              aria-label="Duplicate task"
              onClick={() => onDuplicate(info.row.original.id)}
            >
              <Copy size={14} />
            </button>
            <button
              type="button"
              className="icon-button"
              aria-label="Delete task"
              onClick={() => onDelete(info.row.original.id)}
            >
              <Trash2 size={14} />
            </button>
          </div>
        ),
      }),
    ],
    [projectId, members, customFields, sortField, sortDirection, onSortFieldClick, onDuplicate, onDelete, onOpenDetail],
  );

  const table = useLegacyTable({
    data: tasks,
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
          {tasks.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="task-table__empty-state">
                {isFiltered
                  ? 'No tasks match your search/filters.'
                  : 'No tasks yet. Click "New Task" to add one.'}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
