import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { flexRender } from '@tanstack/react-table';
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { CheckCircle2, FolderOpen, Maximize2 } from 'lucide-react';
import type { Task } from '@/types/task';
import type { MyTaskSortField } from '@/hooks/useMyTasksFilters';
import type { SortDirection } from '@/utils/taskFilters';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { EditableTitleCell } from '@/components/spreadsheet/EditableTitleCell';
import { EditableDateCell } from '@/components/spreadsheet/EditableDateCell';
import { StatusDropdownCell } from '@/components/spreadsheet/StatusDropdownCell';
import { PriorityDropdownCell } from '@/components/spreadsheet/PriorityDropdownCell';
import { SortableColumnHeader } from '@/components/spreadsheet/SortableColumnHeader';
import { formatDateTime } from '@/utils/formatDate';
import '@/components/spreadsheet/TaskTable.css';
import './MyTasksTable.css';

interface MyTasksTableProps {
  tasks: Task[];
  isFiltered: boolean;
  sortField: MyTaskSortField;
  sortDirection: SortDirection;
  onSortFieldClick: (field: MyTaskSortField) => void;
  onOpenDetail: (taskId: string) => void;
}

const columnHelper = legacyCreateColumnHelper<Task>();

// Structurally mirrors TaskTable (same TanStack legacy-table setup, same imported cell
// components) so the two feel like the same grid — the difference is each row carries its
// own project, so every editable cell is called with `task.projectId` instead of a single
// outer projectId, and there's an added Project column/link.
export function MyTasksTable({ tasks, isFiltered, sortField, sortDirection, onSortFieldClick, onOpenDetail }: MyTasksTableProps) {
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
        size: 260,
        minSize: 160,
        cell: (info) => <EditableTitleCell task={info.row.original} projectId={info.row.original.projectId} />,
      }),
      columnHelper.accessor('projectName', {
        header: () => (
          <SortableColumnHeader
            label="Project"
            field="project"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 170,
        minSize: 120,
        cell: (info) => (
          <Link className="my-tasks-table__project-link" to={`/projects/${info.row.original.projectId}`}>
            {info.row.original.projectName ?? 'Unknown project'}
          </Link>
        ),
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
        cell: (info) => <StatusDropdownCell task={info.row.original} projectId={info.row.original.projectId} />,
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
        cell: (info) => <PriorityDropdownCell task={info.row.original} projectId={info.row.original.projectId} />,
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
        cell: (info) => <EditableDateCell task={info.row.original} projectId={info.row.original.projectId} field="dueDate" />,
      }),
      columnHelper.accessor('createdAt', {
        header: () => (
          <SortableColumnHeader
            label="Created"
            field="createdAt"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 110,
        minSize: 100,
        cell: (info) => <span className="my-tasks-table__readonly-date">{formatDateTime(info.getValue())}</span>,
      }),
      columnHelper.accessor('updatedAt', {
        header: () => (
          <SortableColumnHeader
            label="Updated"
            field="updatedAt"
            activeField={sortField}
            direction={sortDirection}
            onClick={onSortFieldClick}
          />
        ),
        size: 110,
        minSize: 100,
        cell: (info) => <span className="my-tasks-table__readonly-date">{formatDateTime(info.getValue())}</span>,
      }),
      columnHelper.display({
        id: 'actions',
        header: '',
        size: 90,
        minSize: 90,
        enableResizing: false,
        cell: (info) => <RowActions task={info.row.original} />,
      }),
    ],
    [sortField, sortDirection, onSortFieldClick, onOpenDetail],
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
                {isFiltered ? 'No tasks match your search/filters.' : "You don't have any assigned tasks."}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

function RowActions({ task }: { task: Task }) {
  const updateTask = useUpdateTask(task.projectId);
  const isComplete = task.status === 'Complete';

  return (
    <div className="task-table__actions">
      <button
        type="button"
        className="icon-button"
        aria-label={isComplete ? 'Already complete' : 'Mark complete'}
        title={isComplete ? 'Already complete' : 'Mark complete'}
        disabled={isComplete || updateTask.isPending}
        onClick={() => updateTask.mutate({ task, change: taskFieldChange.status('Complete') })}
      >
        <CheckCircle2 size={14} />
      </button>
      <Link className="icon-button" aria-label="Open project" title="Open project" to={`/projects/${task.projectId}`}>
        <FolderOpen size={14} />
      </Link>
    </div>
  );
}
