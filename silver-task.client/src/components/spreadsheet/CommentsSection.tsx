import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Paperclip, X } from 'lucide-react';
import type { Comment } from '@/types/comment';
import type { Attachment } from '@/types/attachment';
import { useComments, useCreateComment, useDeleteComment, useUpdateComment } from '@/hooks/useComments';
import { useCommentAttachments } from '@/hooks/useAttachments';
import { attachmentsApi } from '@/api/attachmentsApi';
import { useProject } from '@/hooks/useProjects';
import { useProjectPermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import { ApiError } from '@/api/httpClient';
import { initials } from '@/utils/initials';
import './CommentsSection.css';

interface CommentsSectionProps {
  taskId: string;
  projectId: string;
  currentUserId: string | undefined;
  /** Gates the "add a comment" form only — editing/deleting your *own* existing comment is
   * author-only at the backend (no project-tier check at all, a deliberate design decision from
   * an earlier phase — see CommentService's own doc comment), so it's intentionally not gated
   * here the same way. */
  canEdit: boolean;
}

export function CommentsSection({ taskId, projectId, currentUserId, canEdit }: CommentsSectionProps) {
  const { data: comments } = useComments(taskId);
  const { data: project } = useProject(projectId);
  const { can } = useProjectPermissions(project);
  const canUploadFiles = can(Permissions.FilesUpload);
  const canManageFiles = can(Permissions.FilesDelete);
  const createComment = useCreateComment(taskId);
  const queryClient = useQueryClient();
  const [text, setText] = useState('');
  const [stagedFiles, setStagedFiles] = useState<File[]>([]);
  const [isAttaching, setIsAttaching] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  function handleFileSelected(event: ChangeEvent<HTMLInputElement>) {
    const files = event.target.files ? Array.from(event.target.files) : [];
    event.target.value = '';
    setStagedFiles((prev) => [...prev, ...files]);
  }

  function removeStagedFile(index: number) {
    setStagedFiles((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = text.trim();
    if (!trimmed) {
      return;
    }

    createComment.mutate(trimmed, {
      onSuccess: async (comment) => {
        setText('');
        if (stagedFiles.length > 0) {
          setIsAttaching(true);
          const files = stagedFiles;
          setStagedFiles([]);
          await Promise.allSettled(files.map((file) => attachmentsApi.uploadForComment(comment.id, file)));
          setIsAttaching(false);
          queryClient.invalidateQueries({ queryKey: ['comments', comment.id, 'attachments'] });
          queryClient.invalidateQueries({ queryKey: ['tasks', taskId, 'activities'] });
        }
      },
    });
  }

  return (
    <div className="task-detail-panel__section">
      <h3>Comments{comments && comments.length > 0 ? ` (${comments.length})` : ''}</h3>

      <div className="comment-list">
        {comments?.map((comment) => (
          <CommentRow
            key={comment.id}
            taskId={taskId}
            projectId={projectId}
            comment={comment}
            currentUserId={currentUserId}
            canUploadFiles={canUploadFiles}
            canManageFiles={canManageFiles}
          />
        ))}
        {comments?.length === 0 && <p className="comment-list__empty">No comments yet.</p>}
      </div>

      {canEdit && (
        <form className="comment-form" onSubmit={handleSubmit}>
          <textarea
            placeholder="Add a comment..."
            value={text}
            onChange={(e) => setText(e.target.value)}
            rows={2}
          />

          {stagedFiles.length > 0 && (
            <div className="comment-form__staged">
              {stagedFiles.map((file, index) => (
                <span className="comment-form__staged-chip" key={`${file.name}-${index}`}>
                  {file.name}
                  <button type="button" aria-label={`Remove ${file.name}`} onClick={() => removeStagedFile(index)}>
                    <X size={11} />
                  </button>
                </span>
              ))}
            </div>
          )}

          <div className="comment-form__actions">
            {canUploadFiles && (
              <>
                <input ref={fileInputRef} type="file" multiple className="comment-form__file-input" onChange={handleFileSelected} />
                <button type="button" className="comment-form__attach" onClick={() => fileInputRef.current?.click()}>
                  <Paperclip size={13} />
                  Attach File
                </button>
              </>
            )}
            <button type="submit" disabled={createComment.isPending || isAttaching || !text.trim()}>
              {isAttaching ? 'Attaching...' : 'Comment'}
            </button>
          </div>

          {createComment.isError && (
            <p className="form-error">
              {createComment.error instanceof ApiError ? createComment.error.message : 'Could not post comment.'}
            </p>
          )}
        </form>
      )}
    </div>
  );
}

interface CommentRowProps {
  taskId: string;
  projectId: string;
  comment: Comment;
  currentUserId: string | undefined;
  canUploadFiles: boolean;
  canManageFiles: boolean;
}

function CommentRow({ taskId, projectId, comment, currentUserId, canUploadFiles, canManageFiles }: CommentRowProps) {
  const updateComment = useUpdateComment(taskId);
  const deleteComment = useDeleteComment(taskId);
  const { data: attachments } = useCommentAttachments(comment.id);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [previewing, setPreviewing] = useState<Attachment | null>(null);

  const isOwn = comment.user.id === currentUserId;

  function startEditing() {
    setDraft(comment.text);
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed && trimmed !== comment.text) {
      updateComment.mutate({ id: comment.id, text: trimmed });
    }
  }

  return (
    <div className="comment-row">
      <div className="comment-row__avatar">{initials(comment.user.name)}</div>
      <div className="comment-row__body">
        <div className="comment-row__meta">
          <span className="comment-row__author">{comment.user.name}</span>
          {comment.isAutomated && <span className="comment-row__automation-badge">⚙ Automation</span>}
          <span className="comment-row__date">{new Date(comment.createdAt).toLocaleString()}</span>
        </div>

        {isEditing ? (
          <textarea
            className="comment-row__edit-input"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={commit}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                setIsEditing(false);
              }
            }}
            rows={2}
            autoFocus
          />
        ) : (
          <p className="comment-row__text">{comment.text}</p>
        )}

        {attachments && attachments.length > 0 && (
          <div className="comment-row__attachments">
            {attachments.map((attachment) => (
              <AttachmentRow
                key={attachment.id}
                attachment={attachment}
                currentUserId={currentUserId}
                canUpload={canUploadFiles}
                canManageFiles={canManageFiles}
                onPreview={setPreviewing}
              />
            ))}
          </div>
        )}

        {isOwn && !isEditing && !comment.isAutomated && (
          <div className="comment-row__actions">
            <button type="button" onClick={startEditing}>
              Edit
            </button>
            <button type="button" onClick={() => deleteComment.mutate(comment.id)}>
              Delete
            </button>
          </div>
        )}
      </div>

      {previewing && (
        <FilePreviewModal
          attachment={previewing}
          projectId={projectId}
          currentUserId={currentUserId}
          canUpload={canUploadFiles}
          canManageFiles={canManageFiles}
          onClose={() => setPreviewing(null)}
        />
      )}
    </div>
  );
}
