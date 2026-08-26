import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { attachmentsApi } from '@/api/attachmentsApi';
import type { Attachment, AttachmentFilter } from '@/types/attachment';

const taskAttachmentsKey = (taskId: string) => ['tasks', taskId, 'attachments'] as const;
const activitiesKey = (taskId: string) => ['tasks', taskId, 'activities'] as const;
const projectFilesRootKey = (projectId: string) => ['projects', projectId, 'files'] as const;
const projectFilesKey = (projectId: string, filter: AttachmentFilter) => [...projectFilesRootKey(projectId), filter] as const;
const commentAttachmentsKey = (commentId: string) => ['comments', commentId, 'attachments'] as const;

/** Every rename/delete/restore mutation below is shared across three different list contexts
 * (task, project, comment) — rather than three near-duplicate hooks per action, each takes the
 * full Attachment (which already carries its own projectId/taskId/commentId) so a single mutation
 * can invalidate exactly the caches it affects. */
function invalidateForAttachment(queryClient: ReturnType<typeof useQueryClient>, attachment: Attachment) {
  if (attachment.taskId) {
    queryClient.invalidateQueries({ queryKey: taskAttachmentsKey(attachment.taskId) });
    queryClient.invalidateQueries({ queryKey: activitiesKey(attachment.taskId) });
  }
  if (attachment.projectId) {
    queryClient.invalidateQueries({ queryKey: projectFilesRootKey(attachment.projectId) });
  }
  if (attachment.commentId) {
    queryClient.invalidateQueries({ queryKey: commentAttachmentsKey(attachment.commentId) });
  }
}

export function useTaskAttachments(taskId: string) {
  return useQuery({
    queryKey: taskAttachmentsKey(taskId),
    queryFn: () => attachmentsApi.listForTask(taskId),
  });
}

export function useUploadTaskAttachment(taskId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ file, onProgress }: { file: File; onProgress?: (fraction: number) => void }) =>
      attachmentsApi.uploadForTask(taskId, file, onProgress),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: taskAttachmentsKey(taskId) });
      // Uploads are logged to the activity feed too (Phase 12 infrastructure).
      queryClient.invalidateQueries({ queryKey: activitiesKey(taskId) });
    },
  });
}

export function useProjectFiles(projectId: string, filter: AttachmentFilter) {
  return useQuery({
    queryKey: projectFilesKey(projectId, filter),
    queryFn: () => attachmentsApi.listForProject(projectId, filter),
    placeholderData: (previous) => previous,
  });
}

export function useUploadProjectFile(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ file, onProgress }: { file: File; onProgress?: (fraction: number) => void }) =>
      attachmentsApi.uploadForProject(projectId, file, onProgress),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectFilesRootKey(projectId) });
    },
  });
}

export function useCommentAttachments(commentId: string) {
  return useQuery({
    queryKey: commentAttachmentsKey(commentId),
    queryFn: () => attachmentsApi.listForComment(commentId),
  });
}

export function useUploadCommentAttachment(commentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ file, onProgress }: { file: File; onProgress?: (fraction: number) => void }) =>
      attachmentsApi.uploadForComment(commentId, file, onProgress),
    onSuccess: (attachment) => {
      queryClient.invalidateQueries({ queryKey: commentAttachmentsKey(commentId) });
      if (attachment.taskId) {
        queryClient.invalidateQueries({ queryKey: activitiesKey(attachment.taskId) });
      }
    },
  });
}

export function useRenameAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, fileName }: { attachment: Attachment; fileName: string }) =>
      attachmentsApi.rename(attachment.id, fileName),
    onSuccess: (_updated, { attachment }) => invalidateForAttachment(queryClient, attachment),
  });
}

export function useDeleteAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachment: Attachment) => attachmentsApi.remove(attachment.id),
    onSuccess: (_void, attachment) => invalidateForAttachment(queryClient, attachment),
  });
}

export function useRestoreAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachment: Attachment) => attachmentsApi.restore(attachment.id),
    onSuccess: (_updated, attachment) => invalidateForAttachment(queryClient, attachment),
  });
}
