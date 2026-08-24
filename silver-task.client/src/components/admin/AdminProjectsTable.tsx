import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { flexRender } from '@tanstack/react-table';
import { getCoreRowModel, legacyCreateColumnHelper, useLegacyTable, type LegacyColumnDef } from '@tanstack/react-table/legacy';
import { Archive, ArchiveRestore, FolderOpen, Trash2 } from 'lucide-react';
import type { Project } from '@/types/project';
import { useArchiveProject, useDeleteProjectPermanently, useRestoreProject } from '@/hooks/useProjects';
import { formatDateTime } from '@/utils/formatDate';
import '@/components/spreadsheet/TaskTable.css';
import './AdminProjectsTable.css';

interface AdminProjectsTableProps {
  projects: Project[];
}

const columnHelper = legacyCreateColumnHelper<Project>();

export function AdminProjectsTable({ projects }: AdminProjectsTableProps) {
  const archiveProject = useArchiveProject();
  const restoreProject = useRestoreProject();
  const deleteProject = useDeleteProjectPermanently();

  const columns = useMemo<LegacyColumnDef<Project, any>[]>(
    () => [
      columnHelper.accessor('name', {
        header: 'Project',
        size: 220,
        minSize: 140,
        cell: (info) => (
          <Link className="admin-projects-table__link" to={`/projects/${info.row.original.id}`}>
            {info.getValue()}
          </Link>
        ),
      }),
      columnHelper.accessor((p) => p.owner.name, {
        id: 'owner',
        header: 'Owner',
        size: 160,
        minSize: 120,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue()}</span>,
      }),
      columnHelper.accessor('memberCount', {
        header: 'Members',
        size: 90,
        minSize: 80,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue()}</span>,
      }),
      columnHelper.accessor('taskCount', {
        header: 'Tasks',
        size: 80,
        minSize: 70,
        cell: (info) => <span className="admin-table__readonly-text">{info.getValue() ?? 0}</span>,
      }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        size: 110,
        minSize: 100,
        cell: (info) => <span className="admin-table__readonly-text">{formatDateTime(info.getValue())}</span>,
      }),
      columnHelper.accessor('isArchived', {
        header: 'Status',
        size: 110,
        minSize: 100,
        cell: (info) => (
          <span className={`admin-project-status admin-project-status--${info.getValue() ? 'archived' : 'active'}`}>
            {info.getValue() ? 'Archived' : 'Active'}
          </span>
        ),
      }),
      columnHelper.display({
        id: 'actions',
        header: '',
        size: 120,
        minSize: 120,
        enableResizing: false,
        cell: (info) => {
          const project = info.row.original;
          return (
            <div className="task-table__actions">
              <Link className="icon-button" aria-label="Open project" title="Open project" to={`/projects/${project.id}`}>
                <FolderOpen size={14} />
              </Link>
              {project.isArchived ? (
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Restore project"
                  title="Restore project"
                  disabled={restoreProject.isPending}
                  onClick={() => restoreProject.mutate(project.id)}
                >
                  <ArchiveRestore size={14} />
                </button>
              ) : (
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Archive project"
                  title="Archive project"
                  disabled={archiveProject.isPending}
                  onClick={() => archiveProject.mutate(project.id)}
                >
                  <Archive size={14} />
                </button>
              )}
              <button
                type="button"
                className="icon-button admin-projects-table__delete"
                aria-label="Delete project permanently"
                title="Delete project permanently"
                disabled={deleteProject.isPending}
                onClick={() => {
                  // A hard delete cascades to every task/comment/attachment/custom field on
                  // the project and can't be undone — worth a native confirm even though the
                  // rest of the app's row actions (duplicate/delete a task) don't use one.
                  if (window.confirm(`Permanently delete "${project.name}" and everything in it? This cannot be undone.`)) {
                    deleteProject.mutate(project.id);
                  }
                }}
              >
                <Trash2 size={14} />
              </button>
            </div>
          );
        },
      }),
    ],
    [archiveProject, restoreProject, deleteProject],
  );

  const table = useLegacyTable({
    data: projects,
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
          {projects.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="task-table__empty-state">
                No projects found.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
