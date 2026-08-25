import { useMemo } from 'react';
import { flexRender } from '@tanstack/react-table';
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
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
          {users.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="task-table__empty-state">
                No users yet.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
