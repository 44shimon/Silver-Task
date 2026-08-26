import { useState } from 'react';
import { Download, Pencil, Star, Trash2, X } from 'lucide-react';
import type { Attachment } from '@/types/attachment';
import { attachmentsApi } from '@/api/attachmentsApi';
import { formatFileSize } from '@/utils/formatFileSize';
import { categorizeAttachment } from '@/utils/attachmentType';
import { buildFolderOptions } from '@/utils/folderTree';
import { useFolders } from '@/hooks/useFolders';
import { useActiveFileCategories } from '@/hooks/useFileCategories';
import {
  useAddAttachmentTag,
  useAttachment,
  useDeleteAttachment,
  useMoveAttachment,
  useRemoveAttachmentTag,
  useRenameAttachment,
  useSetAttachmentCategory,
  useToggleFavorite,
  useUpdateAttachmentDescription,
} from '@/hooks/useAttachments';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import './FilePreviewModal.css';

interface FilePreviewModalProps {
  /** The row the user clicked — used only as the initial seed. The modal itself always renders
   * from useAttachment(id), so edits made inside it are reflected immediately instead of showing
   * whatever snapshot the list it was opened from happened to have (see that hook's doc comment). */
  attachment: Attachment;
  /** The file's own project — resolved by the caller (every context already knows it), since a
   * task/comment attachment's own `projectId` field is null (see Attachment's own doc comment);
   * folders and categories are always fetched/assigned relative to this project. */
  projectId: string;
  currentUserId: string | undefined;
  /** Files.Upload (edit-tier) — the uploader or any edit-tier member may rename/move/describe/
   * tag/categorize; canManageFiles (Files.Delete, manage-tier) is required for anyone else. */
  canUpload: boolean;
  canManageFiles: boolean;
  onClose: () => void;
}

const PREVIEWABLE = new Set(['pdf', 'image']);

/** Clicking a file anywhere in the app (task attachments, project files, comment attachments)
 * opens this — the spec's "File Detail" view: preview + every piece of metadata + every
 * authorized action (rename/move/describe/tag/categorize/favorite/delete) in one place, rather
 * than scattering folder-move/tag-management controls across each compact row. No OnlyOffice/
 * office-doc editor exists in this app to preview .doc/.docx/.xls/.xlsx with (confirmed absent —
 * out of scope to build one); those types get metadata + Download only. */
export function FilePreviewModal({ attachment: initialAttachment, projectId, currentUserId, canUpload, canManageFiles, onClose }: FilePreviewModalProps) {
  const { data: attachment } = useAttachment(initialAttachment.id, initialAttachment);
  const category = categorizeAttachment(attachment.mimeType);
  const downloadUrl = attachmentsApi.downloadUrl(attachment.id);
  const canEmbedPreview = PREVIEWABLE.has(category) || attachment.mimeType === 'text/plain';
  const isOwn = attachment.uploadedBy.id === currentUserId;
  const canModify = canManageFiles || (canUpload && isOwn);

  const { data: folders } = useFolders(projectId);
  const { data: categories } = useActiveFileCategories();
  const renameAttachment = useRenameAttachment();
  const moveAttachment = useMoveAttachment();
  const updateDescription = useUpdateAttachmentDescription();
  const setCategory = useSetAttachmentCategory();
  const addTag = useAddAttachmentTag();
  const removeTag = useRemoveAttachmentTag();
  const toggleFavorite = useToggleFavorite();
  const deleteAttachment = useDeleteAttachment();

  const [isEditingName, setIsEditingName] = useState(false);
  const [nameDraft, setNameDraft] = useState(attachment.fileName);
  const [isEditingDescription, setIsEditingDescription] = useState(false);
  const [descriptionDraft, setDescriptionDraft] = useState(attachment.description ?? '');
  const [tagDraft, setTagDraft] = useState('');
  const [tagError, setTagError] = useState<string | null>(null);

  const folderOptions = buildFolderOptions(folders ?? []);
  const categoryOptions = categories ?? [];
  const hasInactiveCurrentCategory = attachment.category && !categoryOptions.some((c) => c.id === attachment.category!.id);

  function startEditingName() {
    setNameDraft(attachment.fileName);
    setIsEditingName(true);
  }

  function commitName() {
    setIsEditingName(false);
    const trimmed = nameDraft.trim();
    if (trimmed && trimmed !== attachment.fileName) {
      renameAttachment.mutate({ attachment, fileName: trimmed });
    }
  }

  function startEditingDescription() {
    setDescriptionDraft(attachment.description ?? '');
    setIsEditingDescription(true);
  }

  function commitDescription() {
    setIsEditingDescription(false);
    const trimmed = descriptionDraft.trim();
    if (trimmed !== (attachment.description ?? '')) {
      updateDescription.mutate({ attachment, description: trimmed || null });
    }
  }

  async function handleAddTag() {
    const trimmed = tagDraft.trim();
    if (!trimmed) {
      return;
    }
    setTagError(null);
    try {
      await addTag.mutateAsync({ attachment, name: trimmed });
      setTagDraft('');
    } catch (error) {
      setTagError(error instanceof ApiError ? error.message : 'Could not add tag.');
    }
  }

  function handleDelete() {
    deleteAttachment.mutate(attachment, { onSuccess: onClose });
  }

  return (
    <Modal onClose={onClose} size="xl">
      <div className="file-preview-modal__header">
        {isEditingName ? (
          <input
            className="file-preview-modal__name-input"
            value={nameDraft}
            onChange={(e) => setNameDraft(e.target.value)}
            onBlur={commitName}
            onKeyDown={(e) => {
              if (e.key === 'Enter') e.currentTarget.blur();
              if (e.key === 'Escape') setIsEditingName(false);
            }}
            autoFocus
          />
        ) : (
          <h2 onClick={() => canModify && startEditingName()} title={canModify ? 'Click to rename' : undefined}>
            {attachment.fileName}
          </h2>
        )}
        <div className="file-preview-modal__header-actions">
          <button
            type="button"
            className={`icon-button${attachment.isFavorite ? ' file-preview-modal__favorite--active' : ''}`}
            aria-label={attachment.isFavorite ? 'Unfavorite' : 'Favorite'}
            onClick={() => toggleFavorite.mutate({ attachment, favorite: !attachment.isFavorite })}
          >
            <Star size={16} fill={attachment.isFavorite ? 'currentColor' : 'none'} />
          </button>
          <a className="icon-button" href={downloadUrl} target="_blank" rel="noopener noreferrer" aria-label="Download">
            <Download size={16} />
          </a>
          {canModify && (
            <button type="button" className="icon-button" aria-label="Rename" onClick={startEditingName}>
              <Pencil size={15} />
            </button>
          )}
          {canModify && (
            <button type="button" className="icon-button" aria-label="Delete" onClick={handleDelete}>
              <Trash2 size={15} />
            </button>
          )}
        </div>
      </div>

      {canEmbedPreview && (
        <div className="file-preview-modal__preview">
          {category === 'image' ? (
            <img src={downloadUrl} alt={attachment.fileName} className="file-preview-modal__image" />
          ) : (
            <iframe src={downloadUrl} title={attachment.fileName} className="file-preview-modal__frame" />
          )}
        </div>
      )}

      <div className="file-preview-modal__field">
        <span className="file-preview-modal__label">Description</span>
        {isEditingDescription ? (
          <textarea
            className="file-preview-modal__description-input"
            value={descriptionDraft}
            onChange={(e) => setDescriptionDraft(e.target.value)}
            onBlur={commitDescription}
            onKeyDown={(e) => e.key === 'Escape' && setIsEditingDescription(false)}
            rows={2}
            autoFocus
          />
        ) : canModify ? (
          <p className="file-preview-modal__description" onClick={startEditingDescription}>
            {attachment.description || <span className="editable-cell__placeholder">Add a description...</span>}
          </p>
        ) : (
          <p className="file-preview-modal__description">{attachment.description || '—'}</p>
        )}
      </div>

      <div className="file-preview-modal__row">
        <div className="file-preview-modal__field">
          <span className="file-preview-modal__label">Category</span>
          <select
            value={attachment.category?.id ?? ''}
            disabled={!canModify}
            onChange={(e) => setCategory.mutate({ attachment, categoryId: e.target.value || null })}
          >
            <option value="">No category</option>
            {hasInactiveCurrentCategory && <option value={attachment.category!.id}>{attachment.category!.name} (inactive)</option>}
            {categoryOptions.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </div>

        <div className="file-preview-modal__field">
          <span className="file-preview-modal__label">Folder</span>
          <select
            value={attachment.folderId ?? ''}
            disabled={!canModify}
            onChange={(e) => moveAttachment.mutate({ attachment, folderId: e.target.value || null })}
          >
            {folderOptions.map((option) => (
              <option key={option.id ?? 'root'} value={option.id ?? ''}>
                {'  '.repeat(option.depth)}
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="file-preview-modal__field">
        <span className="file-preview-modal__label">Tags</span>
        <div className="file-preview-modal__tags">
          {attachment.tags.map((tag) => (
            <span className="tag-chip" key={tag.id} style={tag.color ? { borderColor: tag.color, color: tag.color } : undefined}>
              {tag.name}
              {canModify && (
                <button type="button" aria-label={`Remove tag ${tag.name}`} onClick={() => removeTag.mutate({ attachment, tagId: tag.id })}>
                  <X size={10} />
                </button>
              )}
            </span>
          ))}
          {canModify && (
            <span className="file-preview-modal__add-tag">
              <input
                type="text"
                placeholder="Add tag..."
                value={tagDraft}
                onChange={(e) => setTagDraft(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    handleAddTag();
                  }
                }}
              />
              <button type="button" onClick={handleAddTag} disabled={!tagDraft.trim() || addTag.isPending}>
                Add
              </button>
            </span>
          )}
        </div>
        {tagError && <p className="form-error">{tagError}</p>}
      </div>

      <dl className="file-preview-modal__info">
        <dt>Type</dt>
        <dd>{attachment.mimeType}</dd>
        <dt>Size</dt>
        <dd>{formatFileSize(attachment.fileSize)}</dd>
        <dt>Uploaded by</dt>
        <dd>{attachment.uploadedBy.name}</dd>
        <dt>Uploaded</dt>
        <dd>{new Date(attachment.createdAt).toLocaleString()}</dd>
        <dt>Last modified</dt>
        <dd>{new Date(attachment.updatedAt).toLocaleString()}</dd>
        <dt>Location</dt>
        <dd>{attachment.location}</dd>
      </dl>
    </Modal>
  );
}
