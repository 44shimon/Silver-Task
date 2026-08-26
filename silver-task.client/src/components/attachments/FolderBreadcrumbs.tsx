import { Home } from 'lucide-react';
import type { Folder } from '@/types/folder';
import { getFolderAncestors } from '@/utils/folderTree';
import './FolderBreadcrumbs.css';

interface FolderBreadcrumbsProps {
  currentFolder: Folder | null;
  folders: Folder[];
  onNavigate: (folderId: string | null) => void;
}

export function FolderBreadcrumbs({ currentFolder, folders, onNavigate }: FolderBreadcrumbsProps) {
  const ancestors = currentFolder ? getFolderAncestors(currentFolder, folders) : [];

  return (
    <nav className="folder-breadcrumbs" aria-label="Folder location">
      <button type="button" className="folder-breadcrumbs__item" onClick={() => onNavigate(null)}>
        <Home size={13} />
        Home
      </button>
      {ancestors.map((folder) => (
        <span className="folder-breadcrumbs__segment" key={folder.id}>
          <span className="folder-breadcrumbs__separator">/</span>
          <button type="button" className="folder-breadcrumbs__item" onClick={() => onNavigate(folder.id)}>
            {folder.name}
          </button>
        </span>
      ))}
      {currentFolder && (
        <span className="folder-breadcrumbs__segment">
          <span className="folder-breadcrumbs__separator">/</span>
          <span className="folder-breadcrumbs__current">{currentFolder.name}</span>
        </span>
      )}
    </nav>
  );
}
