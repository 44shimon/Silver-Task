import { useQuery } from '@tanstack/react-query';
import { tasksApi } from '@/api/tasksApi';

export function useTaskActivities(taskId: string) {
  return useQuery({
    queryKey: ['tasks', taskId, 'activities'],
    queryFn: () => tasksApi.activities(taskId),
  });
}
