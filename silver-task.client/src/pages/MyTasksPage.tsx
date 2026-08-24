import { useSearchParams } from 'react-router-dom';
import { useMyTasks } from '@/hooks/useTasks';
import { useMyTasksFilters } from '@/hooks/useMyTasksFilters';
import { useProjects, useProjectMembers } from '@/hooks/useProjects';
import { useCustomFields } from '@/hooks/useCustomFields';
import { useCurrentUser } from '@/hooks/useAuth';
import { MyTasksSummary } from '@/components/dashboard/MyTasksSummary';
import { MyTasksQuickFilters } from '@/components/dashboard/MyTasksQuickFilters';
import { MyTasksFilterPanel } from '@/components/dashboard/MyTasksFilterPanel';
import { MyTasksSortMenu } from '@/components/dashboard/MyTasksSortMenu';
import { MyTasksTable } from '@/components/dashboard/MyTasksTable';
import { TaskSearchInput } from '@/components/spreadsheet/TaskSearchInput';
import { TaskDetailPanel } from '@/components/spreadsheet/TaskDetailPanel';
import './MyTasksPage.css';

const TASK_QUERY_PARAM = 'task';

export function MyTasksPage() {
  const { data: tasks, isLoading, isError } = useMyTasks();
  const { data: projects } = useProjects();
  const { data: currentUser } = useCurrentUser();
  const [searchParams, setSearchParams] = useSearchParams();

  const {
    filteredTasks,
    isFiltered,
    searchQuery,
    setSearchQuery,
    quickFilter,
    setQuickFilter,
    filters,
    setFilters,
    clearFilters,
    activeFilterCount,
    sortField,
    sortDirection,
    setSortField,
    setSortDirection,
    summary,
  } = useMyTasksFilters(tasks ?? []);

  const selectedTaskId = searchParams.get(TASK_QUERY_PARAM);
  const selectedTask = tasks?.find((t) => t.id === selectedTaskId) ?? null;

  // The detail panel needs the selected task's own project members/custom fields — fetched
  // on demand for just that one project, not preloaded for every project on the dashboard.
  const { data: selectedTaskMembers } = useProjectMembers(selectedTask?.projectId);
  const { data: selectedTaskCustomFields } = useCustomFields(selectedTask?.projectId);

  function openTaskDetail(taskId: string) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set(TASK_QUERY_PARAM, taskId);
      return next;
    });
  }

  function closeTaskDetail() {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete(TASK_QUERY_PARAM);
      return next;
    });
  }

  return (
    <div className="my-tasks-page">
      <div className="my-tasks-page__header">
        <h1>My Tasks</h1>
        <p>Every task assigned to you across all of your projects.</p>
      </div>

      <MyTasksSummary summary={summary} quickFilter={quickFilter} onQuickFilterChange={setQuickFilter} />

      <div className="my-tasks-toolbar">
        <MyTasksQuickFilters value={quickFilter} onChange={setQuickFilter} />
        <div className="my-tasks-toolbar__actions">
          <TaskSearchInput value={searchQuery} onChange={setSearchQuery} />
          <MyTasksFilterPanel
            filters={filters}
            onChange={setFilters}
            onClear={clearFilters}
            activeCount={activeFilterCount}
            projects={projects ?? []}
          />
          <MyTasksSortMenu
            sortField={sortField}
            sortDirection={sortDirection}
            onFieldChange={setSortField}
            onDirectionChange={setSortDirection}
          />
        </div>
      </div>

      {isLoading && <p>Loading your tasks...</p>}
      {isError && <p>Your tasks could not be loaded.</p>}

      {!isLoading && !isError && (
        <MyTasksTable
          tasks={filteredTasks}
          isFiltered={isFiltered}
          sortField={sortField}
          sortDirection={sortDirection}
          onSortFieldClick={setSortField}
          onOpenDetail={openTaskDetail}
        />
      )}

      {selectedTask && (
        <TaskDetailPanel
          task={selectedTask}
          projectId={selectedTask.projectId}
          members={selectedTaskMembers?.map((m) => m.user) ?? []}
          customFields={selectedTaskCustomFields ?? []}
          currentUserId={currentUser?.id}
          onClose={closeTaskDetail}
        />
      )}
    </div>
  );
}
