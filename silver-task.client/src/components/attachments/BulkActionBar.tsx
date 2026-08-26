import { useState } from 'react';
import { Download, Star, Tag, Trash2, X } from 'lucide-react';
import type { Attachment } from '@/types/attachment';
import type { Folder } from '@/types/folder';
import { attachmentsApi } from '@/api/attachmentsApi';
import { buildFolderOptions } from '@/utils/folderTree';
import { useBulkDeleteFiles, useBulkFavoriteFiles, useBulkMoveFiles, useBulkTagFiles } from '@/hooks/useAttachments';
import './BulkActionBar.css';

interface BulkActionBarProps {
  selectedIds: string[];
  selectedAttachments: Attachment[];
  folders: Folder[];
  onClear: () => void;
}

/** Multi-select toolbar (Phase 34) — every action re-runs the exact same per-file backend
 * authorization as the single-file version (see BulkActionResultDto's own doc comment); this
 * component only surfaces which of the selection succeeded/failed, it never assumes success. */
export function BulkActionBar({ selectedIds, selectedAttachments, folders, onClear }: BulkActionBarProps) {
  const bulkMove = useBulkMoveFiles();
  const bulkTag = useBulkTagFiles();
  const bulkDelete = useBulkDeleteFiles();
  const bulkFavorite = useBulkFavoriteFiles();
  const [showMove, setShowMove] = useState(false);
  const [showTag, setShowTag] = useState(false);
  const [moveTarget, setMoveTarget] = useState('');
  const [tagName, setTagName] = useState('');
  const [result, setResult] = useState<string | null>(null);

  const folderOptions = buildFolderOptions(folders);

  function reportResult(label: string, failedCount: number) {
    setResult(failedCount === 0 ? `${label}: done.` : `${label}: ${failedCount} file(s) failed (permission or validation).`);
  }

  async function handleMove() {
    const res = await bulkMove.mutateAsync({ fileIds: selectedIds, folderId: moveTarget || null });
    reportResult('Move', res.failed.length);
    setShowMove(false);
  }

  async function handleTag() {
    if (!tagName.trim()) return;
    const res = await bulkTag.mutateAsync({ fileIds: selectedIds, tagName: tagName.trim() });
    reportResult('Tag', res.failed.length);
    setShowTag(false);
    setTagName('');
  }

  async function handleFavorite(favorite: boolean) {
    const res = await bulkFavorite.mutateAsync({ fileIds: selectedIds, favorite });
    reportResult(favorite ? 'Favorite' : 'Unfavorite', res.failed.length);
  }

  async function handleDelete() {
    if (!window.confirm(`This will delete ${selectedIds.length} file(s).`)) {
      return;
    }
    const res = await bulkDelete.mutateAsync(selectedIds);
    reportResult('Delete', res.failed.length);
    onClear();
  }

  function handleDownload() {
    // No server-side ZIP endpoint (Phase 33 deliberately deferred bulk-download complexity) —
    // each file opens its own authenticated download in a new tab instead.
    selectedAttachments.forEach((attachment) => window.open(attachmentsApi.downloadUrl(attachment.id), '_blank'));
  }

  return (
    <div className="bulk-action-bar">
      <span className="bulk-action-bar__count">{selectedIds.length} selected</span>

      <div className="bulk-action-bar__actions">
        <button type="button" onClick={() => setShowMove((prev) => !prev)}>
          Move
        </button>
        <button type="button" onClick={() => setShowTag((prev) => !prev)}>
          <Tag size={13} />
          Tag
        </button>
        <button type="button" onClick={() => handleFavorite(true)}>
          <Star size={13} />
          Favorite
        </button>
        <button type="button" onClick={handleDownload}>
          <Download size={13} />
          Download
        </button>
        <button type="button" className="bulk-action-bar__delete" onClick={handleDelete}>
          <Trash2 size={13} />
          Delete
        </button>
        <button type="button" className="bulk-action-bar__clear" aria-label="Clear selection" onClick={onClear}>
          <X size={14} />
        </button>
      </div>

      {showMove && (
        <div className="bulk-action-bar__popover">
          <select value={moveTarget} onChange={(e) => setMoveTarget(e.target.value)}>
            {folderOptions.map((option) => (
              <option key={option.id ?? 'root'} value={option.id ?? ''}>
                {'  '.repeat(option.depth)}
                {option.label}
              </option>
            ))}
          </select>
          <button type="button" onClick={handleMove} disabled={bulkMove.isPending}>
            Move here
          </button>
        </div>
      )}

      {showTag && (
        <div className="bulk-action-bar__popover">
          <input type="text" placeholder="Tag name" value={tagName} onChange={(e) => setTagName(e.target.value)} />
          <button type="button" onClick={handleTag} disabled={bulkTag.isPending || !tagName.trim()}>
            Apply
          </button>
        </div>
      )}

      {result && <span className="bulk-action-bar__result">{result}</span>}
    </div>
  );
}
