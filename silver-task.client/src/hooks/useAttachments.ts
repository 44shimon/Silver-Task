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
    mutationFn: ({ file, folderId, onProgress }: { file: File; folderId?: string | null; onProgress?: (fraction: number) => void }) =>
      attachmentsApi.uploadForProject(projectId, file, folderId, onProgress),
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

export function useMoveAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, folderId }: { attachment: Attachment; folderId: string | null }) =>
      attachmentsApi.move(attachment.id, folderId),
    onSuccess: (_updated, { attachment }) => invalidateForAttachment(queryClient, attachment),
  });
}

export function useUpdateAttachmentDescription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, description }: { attachment: Attachment; description: string | null }) =>
      attachmentsApi.updateDescription(attachment.id, description),
    onSuccess: (_updated, { attachment }) => invalidateForAttachment(queryClient, attachment),
  });
}

export function useSetAttachmentCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, categoryId }: { attachment: Attachment; categoryId: string | null }) =>
      attachmentsApi.setCategory(attachment.id, categoryId),
    onSuccess: (_updated, { attachment }) => invalidateForAttachment(queryClient, attachment),
  });
}

export function useAddAttachmentTag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, name }: { attachment: Attachment; name: string }) =>
      attachmentsApi.addTag(attachment.id, name),
    onSuccess: (_tag, { attachment }) => {
      invalidateForAttachment(queryClient, attachment);
      queryClient.invalidateQueries({ queryKey: ['tags', 'active'] });
    },
  });
}

export function useRemoveAttachmentTag() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, tagId }: { attachment: Attachment; tagId: string }) =>
      attachmentsApi.removeTag(attachment.id, tagId),
    onSuccess: (_void, { attachment }) => invalidateForAttachment(queryClient, attachment),
  });
}

export function useToggleFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ attachment, favorite }: { attachment: Attachment; favorite: boolean }) =>
      favorite ? attachmentsApi.favorite(attachment.id) : attachmentsApi.unfavorite(attachment.id),
    onSuccess: (_void, { attachment }) => {
      invalidateForAttachment(queryClient, attachment);
      queryClient.invalidateQueries({ queryKey: ['attachments', 'favorites'] });
    },
  });
}

export function useFavoriteFiles() {
  return useQuery({
    queryKey: ['attachments', 'favorites'],
    queryFn: attachmentsApi.listFavorites,
  });
}

export function useRecentFiles(limit = 50) {
  return useQuery({
    queryKey: ['attachments', 'recent', limit],
    queryFn: () => attachmentsApi.listRecent(limit),
  });
}

/** Bulk actions (Phase 34) — broadly invalidate every list-shaped cache rather than trying to
 * compute exactly which projects/tasks/comments a mixed multi-file selection touched; a
 * multi-select action is inherently a "refetch the view" moment, not one worth a precise
 * per-item cache patch. */
function useInvalidateAfterBulkAction() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['projects'] });
    queryClient.invalidateQueries({ queryKey: ['tasks'] });
    queryClient.invalidateQueries({ queryKey: ['comments'] });
    queryClient.invalidateQueries({ queryKey: ['attachments'] });
    queryClient.invalidateQueries({ queryKey: ['tags', 'active'] });
  };
}

export function useBulkMoveFiles() {
  const invalidate = useInvalidateAfterBulkAction();
  return useMutation({
    mutationFn: ({ fileIds, folderId }: { fileIds: string[]; folderId: string | null }) =>
      attachmentsApi.bulkMove(fileIds, folderId),
    onSuccess: invalidate,
  });
}

export function useBulkTagFiles() {
  const invalidate = useInvalidateAfterBulkAction();
  return useMutation({
    mutationFn: ({ fileIds, tagName }: { fileIds: string[]; tagName: string }) => attachmentsApi.bulkTag(fileIds, tagName),
    onSuccess: invalidate,
  });
}

export function useBulkUntagFiles() {
  const invalidate = useInvalidateAfterBulkAction();
  return useMutation({
    mutationFn: ({ fileIds, tagId }: { fileIds: string[]; tagId: string }) => attachmentsApi.bulkUntag(fileIds, tagId),
    onSuccess: invalidate,
  });
}

export function useBulkDeleteFiles() {
  const invalidate = useInvalidateAfterBulkAction();
  return useMutation({
    mutationFn: (fileIds: string[]) => attachmentsApi.bulkDelete(fileIds),
    onSuccess: invalidate,
  });
}

export function useBulkFavoriteFiles() {
  const invalidate = useInvalidateAfterBulkAction();
  return useMutation({
    mutationFn: ({ fileIds, favorite }: { fileIds: string[]; favorite: boolean }) =>
      attachmentsApi.bulkFavorite(fileIds, favorite),
    onSuccess: invalidate,
  });
}
