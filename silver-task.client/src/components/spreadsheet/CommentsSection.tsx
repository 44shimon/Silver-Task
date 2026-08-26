import { useState, type FormEvent } from 'react';
import type { Comment } from '@/types/comment';
import { useComments, useCreateComment, useDeleteComment, useUpdateComment } from '@/hooks/useComments';
import { ApiError } from '@/api/httpClient';
import { initials } from '@/utils/initials';
import './CommentsSection.css';

interface CommentsSectionProps {
  taskId: string;
  currentUserId: string | undefined;
  /** Gates the "add a comment" form only — editing/deleting your *own* existing comment is
   * author-only at the backend (no project-tier check at all, a deliberate design decision from
   * an earlier phase — see CommentService's own doc comment), so it's intentionally not gated
   * here the same way. */
  canEdit: boolean;
}

export function CommentsSection({ taskId, currentUserId, canEdit }: CommentsSectionProps) {
  const { data: comments } = useComments(taskId);
  const createComment = useCreateComment(taskId);
  const [text, setText] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = text.trim();
    if (!trimmed) {
      return;
    }
    createComment.mutate(trimmed, { onSuccess: () => setText('') });
  }

  return (
    <div className="task-detail-panel__section">
      <h3>Comments{comments && comments.length > 0 ? ` (${comments.length})` : ''}</h3>

      <div className="comment-list">
        {comments?.map((comment) => (
          <CommentRow key={comment.id} taskId={taskId} comment={comment} currentUserId={currentUserId} />
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
          <button type="submit" disabled={createComment.isPending || !text.trim()}>
            Comment
          </button>
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
  comment: Comment;
  currentUserId: string | undefined;
}

function CommentRow({ taskId, comment, currentUserId }: CommentRowProps) {
  const updateComment = useUpdateComment(taskId);
  const deleteComment = useDeleteComment(taskId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');

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

        {isOwn && !isEditing && (
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
    </div>
  );
}
