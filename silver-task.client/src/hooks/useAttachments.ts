import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { attachmentsApi } from '@/api/attachmentsApi';

const attachmentsKey = (taskId: string) => ['tasks', taskId, 'attachments'] as const;
const activitiesKey = (taskId: string) => ['tasks', taskId, 'activities'] as const;

export function useAttachments(taskId: string) {
  return useQuery({
    queryKey: attachmentsKey(taskId),
    queryFn: () => attachmentsApi.list(taskId),
  });
}

export function useUploadAttachment(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => attachmentsApi.upload(taskId, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: attachmentsKey(taskId) });
      // Uploads are logged to the activity feed too (Phase 12 infrastructure).
      queryClient.invalidateQueries({ queryKey: activitiesKey(taskId) });
    },
  });
}

export function useDeleteAttachment(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => attachmentsApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: attachmentsKey(taskId) });
      queryClient.invalidateQueries({ queryKey: activitiesKey(taskId) });
    },
  });
}
