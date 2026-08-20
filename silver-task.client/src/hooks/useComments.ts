import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { commentsApi } from '@/api/commentsApi';

const commentsKey = (taskId: string) => ['tasks', taskId, 'comments'] as const;

export function useComments(taskId: string) {
  return useQuery({
    queryKey: commentsKey(taskId),
    queryFn: () => commentsApi.list(taskId),
  });
}

export function useCreateComment(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (text: string) => commentsApi.create(taskId, { text }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: commentsKey(taskId) });
    },
  });
}

export function useUpdateComment(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, text }: { id: string; text: string }) => commentsApi.update(id, { text }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: commentsKey(taskId) });
    },
  });
}

export function useDeleteComment(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => commentsApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: commentsKey(taskId) });
    },
  });
}
