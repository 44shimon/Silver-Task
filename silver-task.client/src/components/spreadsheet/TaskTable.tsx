import { useMemo, useRef, useState } from 'react';
import { flexRender, type ExpandedState } from '@tanstack/react-table';
import { useVirtualizer } from '@tanstack/react-virtual';
// TanStack Table v9 replaced useReactTable with a new modular `features`-based
// useTable API. useLegacyTable is the officially bundled v8-compatible layer —
// used here deliberately (not left over from a migration) since a static grid
// with resizable columns doesn't need v9's tree-shakeable feature system, and
// the classic API is far less code to get right.
import {
  getCoreRowModel,
  getExpandedRowModel,
  legacyCreateColumnHelper,
  useLegacyTable,
  type LegacyColumnDef,
} from '@tanstack/react-table/legacy';
import { ChevronDown, ChevronRight, Copy, Maximize2, Repeat, Trash2 } from 'lucide-react';
import type { Task } from '@/types/task';
import type { UserSummary } from '@/types/project';
import type { CustomField } from '@/types/customField';
import type { TaskSortField } from '@/hooks/useTaskFilters';
import type { SortDirection } from '@/utils/taskFilters';
import { buildTaskTree, type TaskTreeNode } from '@/utils/taskHierarchy';
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
  /** Phase 32 read-only mode — false hides the New Task/expand-into-edit affordances (still
   * shows current values) instead of disabling each control individually. Duplicate is also an
   * edit-tier action (it creates a new task). The backend independently rejects the write either
   * way; this only avoids offering controls that would fail. */
  canEdit: boolean;
  /** Delete is a separate (and often stricter) permission from edit — see Tasks.Delete. */
  canDelete: boolean;
  /** Phase 39 — Permissions.DependenciesOverride for this project; offers the Override option
   * when a status change is rejected as dependency-blocked. */
  canOverrideDependencies?: boolean;
}

const columnHelper = legacyCreateColumnHelper<TaskTreeNode>();

// Phase 60 — row virtualization only kicks in above this threshold. Below it, the table renders
// exactly as it always has (unbounded height, whole-page scroll) — most projects never approach
// this, and there's no reason to change the scroll behavior of a small, typical task list just to
// solve a problem it doesn't have. Above it, only visible rows become real DOM <tr>s, so a
// project with thousands of tasks doesn't render thousands of real rows at once.
const VIRTUALIZATION_ROW_THRESHOLD = 100;
const ESTIMATED_ROW_HEIGHT_PX = 37;

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
  canEdit,
  canDelete,
  canOverrideDependencies,
}: TaskTableProps) {
  // Expanded by default — subtasks should be visible without an extra step the first time a
  // project with hierarchy is opened. Kept in local state (not reloaded from the server), so
  // toggling never refetches anything.
  const [expanded, setExpanded] = useState<ExpandedState>(true);
  const treeData = useMemo(() => buildTaskTree(tasks), [tasks]);

  const columns = useMemo<LegacyColumnDef<TaskTreeNode, any>[]>(
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
        cell: (info) => {
          const row = info.row;
          const task = row.original;
          return (
            <div className="task-table__title-cell" style={{ paddingLeft: row.depth * 18 }}>
              {row.getCanExpand() ? (
                <button
                  type="button"
                  className="task-table__expand-toggle"
                  aria-label={row.getIsExpanded() ? 'Collapse subtasks' : 'Expand subtasks'}
                  onClick={row.getToggleExpandedHandler()}
                >
                  {row.getIsExpanded() ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
                </button>
              ) : (
                <span className="task-table__expand-spacer" />
              )}
              <EditableTitleCell task={task} projectId={projectId} readOnly={!canEdit} />
              {task.recurringTaskId && (
                <span className="task-table__recurring-icon" title="Recurring task">
                  <Repeat size={12} />
                </span>
              )}
              {task.subtaskCount > 0 && (
                <span className="task-table__subtask-count" title={`${task.subtaskCount} subtasks`}>
                  {task.subtaskCount}
                </span>
              )}
            </div>
          );
        },
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
        cell: (info) => (
          <StatusDropdownCell task={info.row.original} projectId={projectId} readOnly={!canEdit} canOverride={canOverrideDependencies} />
        ),
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
        cell: (info) => <PriorityDropdownCell task={info.row.original} projectId={projectId} readOnly={!canEdit} />,
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
          <AssignedToDropdownCell task={info.row.original} projectId={projectId} members={members} readOnly={!canEdit} />
        ),
      }),
      columnHelper.accessor('startDate', {
        header: 'Start Date',
        size: 120,
        minSize: 100,
        cell: (info) => <EditableDateCell task={info.row.original} projectId={projectId} field="startDate" readOnly={!canEdit} />,
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
        cell: (info) => <EditableDateCell task={info.row.original} projectId={projectId} field="dueDate" readOnly={!canEdit} />,
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
            {canEdit && (
              <button
                type="button"
                className="icon-button"
                aria-label="Duplicate task"
                onClick={() => onDuplicate(info.row.original.id)}
              >
                <Copy size={14} />
              </button>
            )}
            {canDelete && (
              <button
                type="button"
                className="icon-button"
                aria-label="Delete task"
                onClick={() => onDelete(info.row.original.id)}
              >
                <Trash2 size={14} />
              </button>
            )}
          </div>
        ),
      }),
    ],
    [
      projectId,
      members,
      customFields,
      sortField,
      sortDirection,
      onSortFieldClick,
      onDuplicate,
      onDelete,
      onOpenDetail,
      canEdit,
      canDelete,
      canOverrideDependencies,
    ],
  );

  const table = useLegacyTable({
    data: treeData,
    columns,
    columnResizeMode: 'onChange',
    getCoreRowModel: getCoreRowModel(),
    getSubRows: (row) => row.subRows,
    getExpandedRowModel: getExpandedRowModel(),
    state: { expanded },
    onExpandedChange: setExpanded,
  });

  const rows = table.getRowModel().rows;
  const shouldVirtualize = rows.length > VIRTUALIZATION_ROW_THRESHOLD;
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  // Always called (Rules of Hooks) — simply unused when shouldVirtualize is false, since
  // scrollContainerRef won't be attached to any DOM node in that branch.
  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollContainerRef.current,
    estimateSize: () => ESTIMATED_ROW_HEIGHT_PX,
    overscan: 10,
    // Row height isn't perfectly fixed (wrapped titles, custom field content) — measure the
    // actual rendered height after each row mounts rather than trusting the estimate forever.
    measureElement: (element) => element.getBoundingClientRect().height,
  });

  const hasSubtasks = tasks.some((t) => t.parentTaskId);

  const headerGroups = (
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
  );

  const emptyStateRow = tasks.length === 0 && (
    <tr>
      <td colSpan={columns.length} className="task-table__empty-state">
        {isFiltered ? 'No tasks match your search/filters.' : 'No tasks yet. Click "New Task" to add one.'}
      </td>
    </tr>
  );

  const hierarchyToolbar = hasSubtasks && (
    <div className="task-table__hierarchy-toolbar">
      <button type="button" className="task-table__hierarchy-toolbar-button" onClick={() => setExpanded(true)}>
        Expand All
      </button>
      <button type="button" className="task-table__hierarchy-toolbar-button" onClick={() => setExpanded({})}>
        Collapse All
      </button>
    </div>
  );

  if (!shouldVirtualize) {
    // Unchanged from before Phase 60 — small/typical task lists render exactly as they always
    // have, whole-page scroll, every row a real <tr>.
    return (
      <div className="task-table-wrapper">
        {hierarchyToolbar}
        <table className="task-table" style={{ width: table.getTotalSize() }}>
          {headerGroups}
          <tbody>
            {rows.map((row) => (
              <tr key={row.id}>
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id} style={{ width: cell.column.getSize() }}>
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))}
            {emptyStateRow}
          </tbody>
        </table>
      </div>
    );
  }

  // Virtualized path (>100 rows): the table body scrolls in its own bounded area instead of the
  // whole page, and only the rows currently in (or near) view become real <tr>s. Two padding rows
  // reserve the vertical space of everything above/below the visible window — the standard table
  // technique for TanStack Virtual, since absolutely-positioning individual <tr>s doesn't compose
  // with normal table layout.
  const virtualRows = virtualizer.getVirtualItems();
  const totalSize = virtualizer.getTotalSize();
  const paddingTop = virtualRows.length > 0 ? virtualRows[0].start : 0;
  const paddingBottom = virtualRows.length > 0 ? totalSize - virtualRows[virtualRows.length - 1].end : 0;

  return (
    <div className="task-table-wrapper">
      {hierarchyToolbar}
      <div ref={scrollContainerRef} className="task-table__scroll-container">
        <table className="task-table" style={{ width: table.getTotalSize() }}>
          {headerGroups}
          <tbody>
            {paddingTop > 0 && (
              <tr aria-hidden style={{ height: paddingTop }}>
                <td colSpan={columns.length} style={{ padding: 0, border: 'none' }} />
              </tr>
            )}
            {virtualRows.map((virtualRow) => {
              const row = rows[virtualRow.index];
              return (
                <tr key={row.id} data-index={virtualRow.index} ref={virtualizer.measureElement}>
                  {row.getVisibleCells().map((cell) => (
                    <td key={cell.id} style={{ width: cell.column.getSize() }}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              );
            })}
            {paddingBottom > 0 && (
              <tr aria-hidden style={{ height: paddingBottom }}>
                <td colSpan={columns.length} style={{ padding: 0, border: 'none' }} />
              </tr>
            )}
            {emptyStateRow}
          </tbody>
        </table>
      </div>
    </div>
  );
}
