import { useNavigate, useSearchParams } from 'react-router-dom';
import { useMyTasks, useTasks } from '@/hooks/useTasks';
import { useMyTasksFilters, MY_TASK_SORT_FIELDS, MY_TASK_SORT_FIELD_LABELS } from '@/hooks/useMyTasksFilters';
import type { QuickFilter } from '@/utils/taskFilters';
import { useProject, useProjects, useProjectMembers } from '@/hooks/useProjects';
import { useCustomFields } from '@/hooks/useCustomFields';
import { useCurrentUser } from '@/hooks/useAuth';
import { useProjectPermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import { MyTasksSummary } from '@/components/dashboard/MyTasksSummary';
import { QuickFilterChips } from '@/components/filters/QuickFilterChips';
import { MyTasksFilterPanel } from '@/components/dashboard/MyTasksFilterPanel';
import { SortMenu } from '@/components/filters/SortMenu';
import { MyTasksTable } from '@/components/dashboard/MyTasksTable';
import { TaskSearchInput } from '@/components/spreadsheet/TaskSearchInput';
import { TaskDetailPanel } from '@/components/spreadsheet/TaskDetailPanel';
import './MyTasksPage.css';

const TASK_QUERY_PARAM = 'task';
const VALID_QUICK_FILTERS: QuickFilter[] = ['all', 'open', 'dueToday', 'dueThisWeek', 'overdue', 'completed'];

export function MyTasksPage() {
  const { data: tasks, isLoading, isError } = useMyTasks();
  const { data: projects } = useProjects();
  const { data: currentUser } = useCurrentUser();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  // Seeds the initial quick filter from ?quickFilter=... (e.g. a dashboard stat card link) —
  // read once on mount, not kept in sync afterward, same "starting point, not two-way binding"
  // behavior the ?view= param already has elsewhere in this app.
  const requestedQuickFilter = searchParams.get('quickFilter');
  const initialQuickFilter = VALID_QUICK_FILTERS.includes(requestedQuickFilter as QuickFilter)
    ? (requestedQuickFilter as QuickFilter)
    : 'all';

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
  } = useMyTasksFilters(tasks ?? [], initialQuickFilter);

  const selectedTaskId = searchParams.get(TASK_QUERY_PARAM);
  const selectedTask = tasks?.find((t) => t.id === selectedTaskId) ?? null;

  // The detail panel needs the selected task's own project members/custom fields/task list —
  // fetched on demand for just that one project, not preloaded for every project on the
  // dashboard.
  const { data: selectedTaskMembers } = useProjectMembers(selectedTask?.projectId);
  const { data: selectedTaskCustomFields } = useCustomFields(selectedTask?.projectId);
  const { data: selectedProjectTasks } = useTasks(selectedTask?.projectId);
  const { data: selectedTaskProject } = useProject(selectedTask?.projectId);
  const { can } = useProjectPermissions(selectedTaskProject);

  // A dependency's counterpart task belongs to the same project (enforced server-side) but may
  // not be assigned to the current user, so it might not exist in `tasks` (My Tasks is scoped to
  // the caller's own assignments) — opening it inline here could silently do nothing. Navigating
  // to the project page instead (same `?task=` convention GlobalSearch already uses) always works.
  function openDependencyDetail(taskId: string) {
    if (selectedTask) {
      navigate(`/projects/${selectedTask.projectId}?task=${taskId}`);
    }
  }

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
        <QuickFilterChips value={quickFilter} onChange={setQuickFilter} />
        <div className="my-tasks-toolbar__actions">
          <TaskSearchInput value={searchQuery} onChange={setSearchQuery} />
          <MyTasksFilterPanel
            filters={filters}
            onChange={setFilters}
            onClear={clearFilters}
            activeCount={activeFilterCount}
            projects={projects ?? []}
          />
          <SortMenu
            sortField={sortField}
            sortDirection={sortDirection}
            fields={MY_TASK_SORT_FIELDS}
            labels={MY_TASK_SORT_FIELD_LABELS}
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
          tasks={selectedProjectTasks ?? []}
          currentUserId={currentUser?.id}
          onClose={closeTaskDetail}
          onOpenDetail={openDependencyDetail}
          canEdit={can(Permissions.TasksEdit)}
        />
      )}
    </div>
  );
}
