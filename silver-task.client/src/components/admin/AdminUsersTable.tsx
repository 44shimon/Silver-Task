import { useMemo, useRef } from 'react';
import { flexRender } from '@tanstack/react-table';
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { useVirtualizer } from '@tanstack/react-virtual';
import type { AdminUser } from '@/types/admin';
import { formatDateTime } from '@/utils/formatDate';
import { EditableUserNameCell } from './EditableUserNameCell';
import { UserRoleDropdownCell } from './UserRoleDropdownCell';
import { UserActiveToggleCell } from './UserActiveToggleCell';
import { ResetPasswordButton } from './ResetPasswordButton';
import { DeleteUserButton } from './DeleteUserButton';
import '@/components/spreadsheet/TaskTable.css';
import '@/components/admin/AdminProjectsTable.css';
import './AdminUsersTable.css';

interface AdminUsersTableProps {
  users: AdminUser[];
  currentUserId: string | undefined;
  onResetPassword: (user: AdminUser) => void;
  onDelete: (user: AdminUser) => void;
}

const columnHelper = legacyCreateColumnHelper<AdminUser>();

// Phase 60 — same conditional row-virtualization pattern as TaskTable.tsx (see its own comment
// for the full reasoning): below this row count the table behaves exactly as it always has.
const VIRTUALIZATION_ROW_THRESHOLD = 100;
const ESTIMATED_ROW_HEIGHT_PX = 37;

export function AdminUsersTable({ users, currentUserId, onResetPassword, onDelete }: AdminUsersTableProps) {
  const columns = useMemo<LegacyColumnDef<AdminUser, any>[]>(
    () => [
      columnHelper.accessor('name', {
        header: 'Name',
        size: 220,
        minSize: 140,
        cell: (info) => <EditableUserNameCell user={info.row.original} />,
      }),
      columnHelper.accessor('email', {
        header: 'Email',
        size: 240,
        minSize: 160,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue()}</span>,
      }),
      columnHelper.accessor('role', {
        header: 'Role',
        size: 160,
        minSize: 130,
        cell: (info) => {
          const user = info.row.original;
          const isSelf = user.id === currentUserId;
          return <UserRoleDropdownCell user={user} disabled={isSelf && user.role === 'Administrator'} />;
        },
      }),
      columnHelper.accessor('isActive', {
        header: 'Active',
        size: 110,
        minSize: 100,
        cell: (info) => {
          const user = info.row.original;
          return <UserActiveToggleCell user={user} disabled={user.id === currentUserId} />;
        },
      }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        size: 110,
        minSize: 100,
        cell: (info) => <span className="admin-table__readonly-text">{formatDateTime(info.getValue())}</span>,
      }),
      columnHelper.accessor('updatedAt', {
        header: 'Last Updated',
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
          const user = info.row.original;
          return (
            <div className="task-table__actions">
              <ResetPasswordButton userName={user.name} onClick={() => onResetPassword(user)} />
              <DeleteUserButton userName={user.name} disabled={user.id === currentUserId} onClick={() => onDelete(user)} />
            </div>
          );
        },
      }),
    ],
    [currentUserId, onResetPassword, onDelete],
  );

  const table = useLegacyTable({
    data: users,
    columns,
    columnResizeMode: 'onChange',
    getCoreRowModel: getCoreRowModel(),
  });

  const rows = table.getRowModel().rows;
  const shouldVirtualize = rows.length > VIRTUALIZATION_ROW_THRESHOLD;
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollContainerRef.current,
    estimateSize: () => ESTIMATED_ROW_HEIGHT_PX,
    overscan: 10,
    measureElement: (element) => element.getBoundingClientRect().height,
  });

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

  const emptyStateRow = users.length === 0 && (
    <tr>
      <td colSpan={columns.length} className="task-table__empty-state">
        No users yet.
      </td>
    </tr>
  );

  if (!shouldVirtualize) {
    return (
      <div className="task-table-wrapper">
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

  const virtualRows = virtualizer.getVirtualItems();
  const totalSize = virtualizer.getTotalSize();
  const paddingTop = virtualRows.length > 0 ? virtualRows[0].start : 0;
  const paddingBottom = virtualRows.length > 0 ? totalSize - virtualRows[virtualRows.length - 1].end : 0;

  return (
    <div className="task-table-wrapper">
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
