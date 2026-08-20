import { useMemo } from 'react';
import { flexRender } from '@tanstack/react-table';
// TanStack Table v9 replaced useReactTable with a new modular `features`-based
// useTable API. useLegacyTable is the officially bundled v8-compatible layer —
// used here deliberately (not left over from a migration) since a static grid
// with resizable columns doesn't need v9's tree-shakeable feature system, and
// the classic API is far less code to get right.
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { Copy, Trash2 } from 'lucide-react';
import type { Task } from '@/types/task';
import { StatusBadge } from './StatusBadge';
import { PriorityBadge } from './PriorityBadge';
import './TaskTable.css';

interface TaskTableProps {
  tasks: Task[];
  onDuplicate: (taskId: string) => void;
  onDelete: (taskId: string) => void;
}

const columnHelper = legacyCreateColumnHelper<Task>();

export function TaskTable({ tasks, onDuplicate, onDelete }: TaskTableProps) {
  const columns = useMemo<LegacyColumnDef<Task, any>[]>(
    () => [
      columnHelper.accessor('title', {
        header: 'Task',
        size: 280,
        minSize: 160,
      }),
      columnHelper.accessor('status', {
        header: 'Status',
        size: 140,
        minSize: 120,
        cell: (info) => <StatusBadge status={info.getValue()} />,
      }),
      columnHelper.accessor('priority', {
        header: 'Priority',
        size: 120,
        minSize: 100,
        cell: (info) => <PriorityBadge priority={info.getValue()} />,
      }),
      columnHelper.accessor((task) => task.assignedTo?.name ?? '', {
        id: 'assignedTo',
        header: 'Assigned To',
        size: 160,
        minSize: 120,
        cell: (info) => info.getValue() || <span className="task-table__empty">Unassigned</span>,
      }),
      columnHelper.accessor('startDate', {
        header: 'Start Date',
        size: 120,
        minSize: 100,
        cell: (info) => formatDate(info.getValue()),
      }),
      columnHelper.accessor('dueDate', {
        header: 'Due Date',
        size: 120,
        minSize: 100,
        cell: (info) => formatDate(info.getValue()),
      }),
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
    [onDuplicate, onDelete],
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
                No tasks yet. Click &ldquo;New Task&rdquo; to add one.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

/** Parses a DateOnly ("YYYY-MM-DD") string using local date components, avoiding the
 * off-by-one shift that `new Date(dateOnlyString)` can produce (it's parsed as UTC
 * midnight, then rendered in the local timezone). */
function formatDate(value: string | null): string {
  if (!value) {
    return '';
  }
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day).toLocaleDateString();
}
