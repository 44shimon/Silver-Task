import { useMemo, useState } from 'react';
import { LayoutGrid, List, Plus, Search, X } from 'lucide-react';
import { useProjectFiles, useUploadProjectFile } from '@/hooks/useAttachments';
import { useFolders } from '@/hooks/useFolders';
import { useActiveFileCategories } from '@/hooks/useFileCategories';
import { useActiveTags } from '@/hooks/useTags';
import { useCurrentUser } from '@/hooks/useAuth';
import type { UserSummary } from '@/types/project';
import type {
  Attachment,
  AttachmentDateFilter,
  AttachmentFilter,
  AttachmentSortField,
  AttachmentTypeFilter,
} from '@/types/attachment';
import { ATTACHMENT_TYPE_LABELS, categorizeAttachment } from '@/utils/attachmentType';
import { getFolderChildren } from '@/utils/folderTree';
import { attachmentsApi } from '@/api/attachmentsApi';
import { FileDropzone } from '@/components/attachments/FileDropzone';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { AttachmentIcon } from '@/components/attachments/AttachmentIcon';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import { FolderTree } from '@/components/attachments/FolderTree';
import { FolderBreadcrumbs } from '@/components/attachments/FolderBreadcrumbs';
import { FolderRow } from '@/components/attachments/FolderRow';
import { NewFolderDialog } from '@/components/attachments/NewFolderDialog';
import { BulkActionBar } from '@/components/attachments/BulkActionBar';
import { formatFileSize } from '@/utils/formatFileSize';
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
  const { data: folders } = useFolders(projectId);
  const { data: categories } = useActiveFileCategories();
  const { data: tags } = useActiveTags();

  const [currentFolderId, setCurrentFolderId] = useState<string | null>(null);
  const [searchWholeProject, setSearchWholeProject] = useState(false);
  const [search, setSearch] = useState('');
  const [type, setType] = useState<AttachmentTypeFilter>('all');
  const [uploadedByUserId, setUploadedByUserId] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [tagId, setTagId] = useState('');
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [dateFilter, setDateFilter] = useState<AttachmentDateFilter>('all');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');
  const [onlyDeleted, setOnlyDeleted] = useState(false);
  const [sortField, setSortField] = useState<AttachmentSortField>('date');
  const [sortDescending, setSortDescending] = useState(true);
  const [page, setPage] = useState(1);
  const [viewMode, setViewMode] = useState<'list' | 'grid'>('list');
  const [previewing, setPreviewing] = useState<Attachment | null>(null);
  const [showNewFolder, setShowNewFolder] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  const uploadFile = useUploadProjectFile(projectId);
  const currentFolder = (folders ?? []).find((f) => f.id === currentFolderId) ?? null;
  const subfolders = getFolderChildren(folders ?? [], currentFolderId);
  const isSearching = search.trim().length > 0;

  const filter: AttachmentFilter = useMemo(
    () => ({
      search: search.trim() || undefined,
      type,
      uploadedByUserId: uploadedByUserId || undefined,
      categoryId: categoryId || undefined,
      tagId: tagId || undefined,
      favoritesOnly: favoritesOnly || undefined,
      onlyDeleted,
      sortField,
      sortDescending,
      page,
      pageSize: PAGE_SIZE,
      folderId: currentFolderId ?? undefined,
      includeSubfolders: isSearching && searchWholeProject,
      ...resolveDateRange(dateFilter, customFrom, customTo),
    }),
    [
      search,
      type,
      uploadedByUserId,
      categoryId,
      tagId,
      favoritesOnly,
      onlyDeleted,
      sortField,
      sortDescending,
      page,
      currentFolderId,
      isSearching,
      searchWholeProject,
      dateFilter,
      customFrom,
      customTo,
    ],
  );

  const { data, isLoading, isError } = useProjectFiles(projectId, filter);
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  function updateFilterAndResetPage<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  function navigateToFolder(folderId: string | null) {
    setCurrentFolderId(folderId);
    setPage(1);
    setSelectedIds([]);
  }

  function toggleSelect(id: string) {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((existing) => existing !== id) : [...prev, id]));
  }

  const selectedAttachments = (data?.items ?? []).filter((a) => selectedIds.includes(a.id));

  return (
    <div className="project-files-view">
      <div className="project-files-view__body">
        <aside className="project-files-view__sidebar">
          <FolderTree folders={folders ?? []} currentFolderId={currentFolderId} onNavigate={navigateToFolder} />
        </aside>

        <div className="project-files-view__main">
          <div className="project-files-view__nav-row">
            <FolderBreadcrumbs currentFolder={currentFolder} folders={folders ?? []} onNavigate={navigateToFolder} />
            <div className="project-files-view__view-toggle">
              <button
                type="button"
                className={viewMode === 'list' ? 'project-files-view__view-toggle--active' : ''}
                aria-label="List view"
                onClick={() => setViewMode('list')}
              >
                <List size={14} />
              </button>
              <button
                type="button"
                className={viewMode === 'grid' ? 'project-files-view__view-toggle--active' : ''}
                aria-label="Grid view"
                onClick={() => setViewMode('grid')}
              >
                <LayoutGrid size={14} />
              </button>
            </div>
          </div>

          {canUploadFiles && (
            <div className="project-files-view__upload-row">
              <FileDropzone
                onUpload={(file, onProgress) => uploadFile.mutateAsync({ file, folderId: currentFolderId, onProgress })}
              />
              <button type="button" className="project-files-view__new-folder" onClick={() => setShowNewFolder(true)}>
                <Plus size={13} />
                New Folder
              </button>
            </div>
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

            {isSearching && (
              <label className="project-files-view__deleted-toggle">
                <input
                  type="checkbox"
                  checked={searchWholeProject}
                  onChange={(e) => updateFilterAndResetPage(setSearchWholeProject)(e.target.checked)}
                />
                Include subfolders
              </label>
            )}

            <select value={type} onChange={(e) => updateFilterAndResetPage(setType)(e.target.value as AttachmentTypeFilter)}>
              {TYPE_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {ATTACHMENT_TYPE_LABELS[option]}
                </option>
              ))}
            </select>

            <select value={categoryId} onChange={(e) => updateFilterAndResetPage(setCategoryId)(e.target.value)}>
              <option value="">Any category</option>
              {(categories ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>

            <select value={tagId} onChange={(e) => updateFilterAndResetPage(setTagId)(e.target.value)}>
              <option value="">Any tag</option>
              {(tags ?? []).map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
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

            <label className="project-files-view__deleted-toggle">
              <input type="checkbox" checked={favoritesOnly} onChange={(e) => updateFilterAndResetPage(setFavoritesOnly)(e.target.checked)} />
              Favorites only
            </label>

            {canManageFiles && (
              <label className="project-files-view__deleted-toggle">
                <input type="checkbox" checked={onlyDeleted} onChange={(e) => updateFilterAndResetPage(setOnlyDeleted)(e.target.checked)} />
                Show deleted
              </label>
            )}
          </div>

          {selectedIds.length > 0 && (
            <BulkActionBar
              selectedIds={selectedIds}
              selectedAttachments={selectedAttachments}
              folders={folders ?? []}
              onClear={() => setSelectedIds([])}
            />
          )}

          {!isSearching && subfolders.length > 0 && (
            <div className="attachment-list">
              {subfolders.map((folder) => (
                <FolderRow
                  key={folder.id}
                  folder={folder}
                  folders={folders ?? []}
                  projectId={projectId}
                  currentUserId={currentUser?.id}
                  canUpload={canUploadFiles}
                  canManageFiles={canManageFiles}
                  onOpen={navigateToFolder}
                />
              ))}
            </div>
          )}

          {isLoading && <p>Loading files...</p>}
          {isError && <p className="form-error">Files could not be loaded.</p>}

          {!isLoading && !isError && data?.items.length === 0 && subfolders.length === 0 && (
            <p className="attachment-list__empty">{onlyDeleted ? 'No deleted files.' : 'No files yet.'}</p>
          )}

          {!isLoading && !isError && data && data.items.length > 0 && viewMode === 'list' && (
            <div className="attachment-list">
              {data.items.map((attachment) => (
                <AttachmentRow
                  key={attachment.id}
                  attachment={attachment}
                  currentUserId={currentUser?.id}
                  canUpload={canUploadFiles}
                  canManageFiles={canManageFiles}
                  onPreview={setPreviewing}
                  showLocation={isSearching && searchWholeProject}
                  selected={selectedIds.includes(attachment.id)}
                  onToggleSelect={() => toggleSelect(attachment.id)}
                />
              ))}
            </div>
          )}

          {!isLoading && !isError && data && data.items.length > 0 && viewMode === 'grid' && (
            <div className="project-files-view__grid">
              {data.items.map((attachment) => (
                <button type="button" key={attachment.id} className="project-files-view__grid-card" onClick={() => setPreviewing(attachment)}>
                  {categorizeAttachment(attachment.mimeType) === 'image' ? (
                    <img src={attachmentsApi.downloadUrl(attachment.id)} alt={attachment.fileName} />
                  ) : (
                    <AttachmentIcon mimeType={attachment.mimeType} size={32} />
                  )}
                  <span className="project-files-view__grid-name">{attachment.fileName}</span>
                  <span className="project-files-view__grid-size">{formatFileSize(attachment.fileSize)}</span>
                </button>
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
        </div>
      </div>

      {previewing && (
        <FilePreviewModal
          attachment={previewing}
          projectId={projectId}
          currentUserId={currentUser?.id}
          canUpload={canUploadFiles}
          canManageFiles={canManageFiles}
          onClose={() => setPreviewing(null)}
        />
      )}

      {showNewFolder && (
        <NewFolderDialog projectId={projectId} parentFolderId={currentFolderId} onClose={() => setShowNewFolder(false)} />
      )}
    </div>
  );
}
