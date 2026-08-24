import { useMemo, useState } from 'react';
import { useAllProjectsForAdmin } from '@/hooks/useProjects';
import { AdminProjectsTable } from '@/components/admin/AdminProjectsTable';
import { NewProjectForm } from '@/components/admin/NewProjectForm';
import './AdminProjectsPage.css';

type StatusFilter = 'all' | 'active' | 'archived';

export function AdminProjectsPage() {
  const { data: projects, isLoading, isError } = useAllProjectsForAdmin();
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');

  const filteredProjects = useMemo(() => {
    if (!projects) {
      return [];
    }
    if (statusFilter === 'active') {
      return projects.filter((p) => !p.isArchived);
    }
    if (statusFilter === 'archived') {
      return projects.filter((p) => p.isArchived);
    }
    return projects;
  }, [projects, statusFilter]);

  return (
    <div className="admin-projects-page">
      <div className="admin-projects-page__toolbar">
        <select
          className="admin-projects-page__status-filter"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
        >
          <option value="all">All projects</option>
          <option value="active">Active only</option>
          <option value="archived">Archived only</option>
        </select>
        <NewProjectForm />
      </div>

      {isLoading && <p>Loading projects...</p>}
      {isError && <p>Projects could not be loaded.</p>}

      {!isLoading && !isError && <AdminProjectsTable projects={filteredProjects} />}
    </div>
  );
}
