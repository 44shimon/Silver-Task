import { useMemo, useState } from 'react';
import { Search, X } from 'lucide-react';
import { useProjectFiles, useUploadProjectFile } from '@/hooks/useAttachments';
import { useCurrentUser } from '@/hooks/useAuth';
import type { UserSummary } from '@/types/project';
import type {
  Attachment,
  AttachmentDateFilter,
  AttachmentFilter,
  AttachmentSortField,
  AttachmentTypeFilter,
} from '@/types/attachment';
import { ATTACHMENT_TYPE_LABELS } from '@/utils/attachmentType';
import { FileDropzone } from '@/components/attachments/FileDropzone';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import '@/components/spreadsheet/Toolbar.css';
import './ProjectFilesView.css';

const PAGE_SIZE = 25;
const TYPE_OPTIONS: AttachmentTypeFilter[] = ['all', 'pdf', 'image', 'spreadsheet', 'document', 'archive', 'other'];
const DATE_OPTIONS: { value: AttachmentDateFilter; label: string }[] = [
  { value: 'all', label: 'Any time' },
  { value: 'today', label: 'Today' },
  { value: '7days', label: 'Last 7 days' },
  { value: '30days', label: 'Last 30 days' },
  { value: 'custom', label: 'Custom range' },
];
const SORT_OPTIONS: { value: AttachmentSortField; label: string }[] = [
  { value: 'date', label: 'Date uploaded' },
  { value: 'name', label: 'Name' },
  { value: 'size', label: 'Size' },
  { value: 'type', label: 'Type' },
  { value: 'uploadedBy', label: 'Uploaded by' },
];

function resolveDateRange(filter: AttachmentDateFilter, customFrom: string, customTo: string): { dateFrom?: string; dateTo?: string } {
  const now = new Date();
  if (filter === 'today') {
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    return { dateFrom: start.toISOString() };
  }
  if (filter === '7days') {
    return { dateFrom: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString() };
  }
  if (filter === '30days') {
    return { dateFrom: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString() };
  }
  if (filter === 'custom') {
    return {
      dateFrom: customFrom ? new Date(customFrom).toISOString() : undefined,
      dateTo: customTo ? new Date(`${customTo}T23:59:59`).toISOString() : undefined,
    };
  }
  return {};
}

interface ProjectFilesViewProps {
  projectId: string;
  members: UserSummary[];
  canUploadFiles: boolean;
  canManageFiles: boolean;
}

export function ProjectFilesView({ projectId, members, canUploadFiles, canManageFiles }: ProjectFilesViewProps) {
  const { data: currentUser } = useCurrentUser();
  const [search, setSearch] = useState('');
  const [type, setType] = useState<AttachmentTypeFilter>('all');
  const [uploadedByUserId, setUploadedByUserId] = useState('');
  const [dateFilter, setDateFilter] = useState<AttachmentDateFilter>('all');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');
  const [onlyDeleted, setOnlyDeleted] = useState(false);
  const [sortField, setSortField] = useState<AttachmentSortField>('date');
  const [sortDescending, setSortDescending] = useState(true);
  const [page, setPage] = useState(1);
  const [previewing, setPreviewing] = useState<Attachment | null>(null);

  const uploadFile = useUploadProjectFile(projectId);

  const filter: AttachmentFilter = useMemo(
    () => ({
      search: search.trim() || undefined,
      type,
      uploadedByUserId: uploadedByUserId || undefined,
      onlyDeleted,
      sortField,
      sortDescending,
      page,
      pageSize: PAGE_SIZE,
      ...resolveDateRange(dateFilter, customFrom, customTo),
    }),
    [search, type, uploadedByUserId, onlyDeleted, sortField, sortDescending, page, dateFilter, customFrom, customTo],
  );

  const { data, isLoading, isError } = useProjectFiles(projectId, filter);
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  function updateFilterAndResetPage<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <div className="project-files-view">
      {canUploadFiles && (
        <FileDropzone onUpload={(file, onProgress) => uploadFile.mutateAsync({ file, onProgress })} />
      )}

      <div className="project-files-view__toolbar">
        <div className="task-search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search files..."
            value={search}
            onChange={(e) => updateFilterAndResetPage(setSearch)(e.target.value)}
          />
          {search && (
            <button type="button" className="task-search__clear" aria-label="Clear search" onClick={() => setSearch('')}>
              <X size={13} />
            </button>
          )}
        </div>

        <select value={type} onChange={(e) => updateFilterAndResetPage(setType)(e.target.value as AttachmentTypeFilter)}>
          {TYPE_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {ATTACHMENT_TYPE_LABELS[option]}
            </option>
          ))}
        </select>

        <select value={uploadedByUserId} onChange={(e) => updateFilterAndResetPage(setUploadedByUserId)(e.target.value)}>
          <option value="">Anyone</option>
          {members.map((member) => (
            <option key={member.id} value={member.id}>
              {member.name}
            </option>
          ))}
        </select>

        <select value={dateFilter} onChange={(e) => updateFilterAndResetPage(setDateFilter)(e.target.value as AttachmentDateFilter)}>
          {DATE_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>

        {dateFilter === 'custom' && (
          <>
            <input type="date" value={customFrom} onChange={(e) => updateFilterAndResetPage(setCustomFrom)(e.target.value)} />
            <input type="date" value={customTo} onChange={(e) => updateFilterAndResetPage(setCustomTo)(e.target.value)} />
          </>
        )}

        <select value={sortField} onChange={(e) => setSortField(e.target.value as AttachmentSortField)}>
          {SORT_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              Sort: {option.label}
            </option>
          ))}
        </select>

        <button
          type="button"
          className="project-files-view__sort-direction"
          aria-label={sortDescending ? 'Sort descending' : 'Sort ascending'}
          onClick={() => setSortDescending((prev) => !prev)}
        >
          {sortDescending ? '↓' : '↑'}
        </button>

        {canManageFiles && (
          <label className="project-files-view__deleted-toggle">
            <input type="checkbox" checked={onlyDeleted} onChange={(e) => updateFilterAndResetPage(setOnlyDeleted)(e.target.checked)} />
            Show deleted
          </label>
        )}
      </div>

      {isLoading && <p>Loading files...</p>}
      {isError && <p className="form-error">Files could not be loaded.</p>}

      {!isLoading && !isError && data?.items.length === 0 && (
        <p className="attachment-list__empty">{onlyDeleted ? 'No deleted files.' : 'No files yet.'}</p>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <div className="attachment-list">
          {data.items.map((attachment) => (
            <AttachmentRow
              key={attachment.id}
              attachment={attachment}
              currentUserId={currentUser?.id}
              canUpload={canUploadFiles}
              canManageFiles={canManageFiles}
              onPreview={setPreviewing}
              showLocation
            />
          ))}
        </div>
      )}

      {data && data.totalCount > PAGE_SIZE && (
        <div className="project-files-view__pagination">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </button>
          <span>
            Page {page} of {totalPages}
          </span>
          <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            Next
          </button>
        </div>
      )}

      {previewing && <FilePreviewModal attachment={previewing} onClose={() => setPreviewing(null)} />}
    </div>
  );
}
