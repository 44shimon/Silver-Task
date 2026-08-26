import { useState } from 'react';
import { ChevronDown, ChevronRight, Folder as FolderIcon, Home } from 'lucide-react';
import type { Folder } from '@/types/folder';
import { getFolderChildren } from '@/utils/folderTree';
import './FolderTree.css';

interface FolderTreeProps {
  folders: Folder[];
  currentFolderId: string | null;
  onNavigate: (folderId: string | null) => void;
}

/** Left-sidebar folder tree (Phase 34) — kept deliberately simple (expand/collapse + click to
 * navigate only, no drag-drop reparenting here) per the spec's own "do not make navigation
 * unnecessarily complicated" note; moving a folder is a separate explicit action (see
 * MoveFolderDialog), not a sidebar drag target. */
export function FolderTree({ folders, currentFolderId, onNavigate }: FolderTreeProps) {
  return (
    <nav className="folder-tree" aria-label="Folders">
      <button
        type="button"
        className={`folder-tree__item folder-tree__item--root${currentFolderId === null ? ' folder-tree__item--active' : ''}`}
        onClick={() => onNavigate(null)}
      >
        <Home size={13} />
        Home
      </button>
      {getFolderChildren(folders, null).map((folder) => (
        <FolderTreeNode key={folder.id} folder={folder} folders={folders} currentFolderId={currentFolderId} onNavigate={onNavigate} depth={1} />
      ))}
    </nav>
  );
}

function FolderTreeNode({
  folder,
  folders,
  currentFolderId,
  onNavigate,
  depth,
}: {
  folder: Folder;
  folders: Folder[];
  currentFolderId: string | null;
  onNavigate: (folderId: string | null) => void;
  depth: number;
}) {
  const children = getFolderChildren(folders, folder.id);
  const [isExpanded, setIsExpanded] = useState(false);

  return (
    <div>
      <div className="folder-tree__row" style={{ paddingLeft: `${depth * 14}px` }}>
        {children.length > 0 ? (
          <button
            type="button"
            className="folder-tree__toggle"
            aria-label={isExpanded ? `Collapse ${folder.name}` : `Expand ${folder.name}`}
            onClick={() => setIsExpanded((prev) => !prev)}
          >
            {isExpanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </button>
        ) : (
          <span className="folder-tree__toggle-spacer" />
        )}
        <button
          type="button"
          className={`folder-tree__item${currentFolderId === folder.id ? ' folder-tree__item--active' : ''}`}
          onClick={() => onNavigate(folder.id)}
        >
          <FolderIcon size={13} />
          {folder.name}
        </button>
      </div>
      {isExpanded &&
        children.map((child) => (
          <FolderTreeNode key={child.id} folder={child} folders={folders} currentFolderId={currentFolderId} onNavigate={onNavigate} depth={depth + 1} />
        ))}
    </div>
  );
}
