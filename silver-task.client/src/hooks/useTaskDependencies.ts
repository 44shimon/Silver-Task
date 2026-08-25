import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { dependenciesApi } from '@/api/dependenciesApi';

const dependenciesKey = (taskId: string) => ['tasks', taskId, 'dependencies'] as const;
const dependentsKey = (taskId: string) => ['tasks', taskId, 'dependents'] as const;
const projectEdgesKey = (projectId: string) => ['projects', projectId, 'dependencies'] as const;

export function useTaskDependencies(taskId: string | undefined) {
  return useQuery({
    queryKey: dependenciesKey(taskId ?? ''),
    queryFn: () => dependenciesApi.listDependencies(taskId!),
    enabled: Boolean(taskId),
  });
}

export function useTaskDependents(taskId: string | undefined) {
  return useQuery({
    queryKey: dependentsKey(taskId ?? ''),
    queryFn: () => dependenciesApi.listDependents(taskId!),
    enabled: Boolean(taskId),
  });
}

/** Backs Gantt/Timeline connector lines — one request for the whole project's dependency graph. */
export function useProjectDependencyEdges(projectId: string | undefined) {
  return useQuery({
    queryKey: projectEdgesKey(projectId ?? ''),
    queryFn: () => dependenciesApi.listProjectEdges(projectId!),
    enabled: Boolean(projectId),
  });
}

/** Creating/removing a dependency changes counts (dependsOnCount/blockedByCount/dependentCount)
 * on up to two tasks — this one and its prerequisite/dependent — plus the project-wide edge list
 * Gantt/Timeline read. Rather than tracking exactly which cached queries that touches, both
 * mutations below invalidate the whole `tasks` key prefix (covers every per-task dependency
 * query, the project task list, My Tasks, and search) plus this project's edge list — dependency
 * changes are relatively rare compared to a field edit, so the extra invalidation breadth costs
 * little and avoids subtle staleness bugs. */
function invalidateDependencyData(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  queryClient.invalidateQueries({ queryKey: ['tasks'] });
  queryClient.invalidateQueries({ queryKey: projectEdgesKey(projectId) });
}

export function useCreateTaskDependency(taskId: string, projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dependsOnTaskId: string) => dependenciesApi.create(taskId, dependsOnTaskId),
    onSuccess: () => invalidateDependencyData(queryClient, projectId),
  });
}

/** Takes the target task id per call rather than binding one at hook-creation time — a
 * dependency row's DELETE route must use *its own* TaskId (the dependent task), which for the
 * "Blocking" list is a different task than the one whose panel is currently open (there, the
 * row's TaskId is the prerequisite instead — see TaskDependencyDto/ToDependsOnDto vs
 * ToDependentDto). Passing it explicitly avoids ever calling the wrong route. */
export function useDeleteTaskDependency(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ taskId, dependencyId }: { taskId: string; dependencyId: string }) =>
      dependenciesApi.remove(taskId, dependencyId),
    onSuccess: () => invalidateDependencyData(queryClient, projectId),
  });
}
