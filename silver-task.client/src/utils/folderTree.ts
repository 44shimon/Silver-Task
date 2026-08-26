import type { Folder } from '@/types/folder';

/** Walks ParentFolderId links from a folder up to the root, root-first — same shape as
 * getTaskAncestors (Phase 30), used for both breadcrumbs and the "Invoices / Electrical" style
 * flattened labels the folder-picker select shows. */
export function getFolderAncestors(folder: Folder, folders: Folder[]): Folder[] {
  const byId = new Map(folders.map((f) => [f.id, f]));
  const ancestors: Folder[] = [];
  let currentParentId = folder.parentFolderId;
  const guard = new Set<string>();

  while (currentParentId && !guard.has(currentParentId)) {
    guard.add(currentParentId);
    const parent = byId.get(currentParentId);
    if (!parent) break;
    ancestors.unshift(parent);
    currentParentId = parent.parentFolderId;
  }

  return ancestors;
}

export function getFolderPath(folder: Folder, folders: Folder[]): string {
  const ancestors = getFolderAncestors(folder, folders);
  return [...ancestors, folder].map((f) => f.name).join(' / ');
}

export function getFolderChildren(folders: Folder[], parentFolderId: string | null): Folder[] {
  return folders.filter((f) => f.parentFolderId === parentFolderId).sort((a, b) => a.name.localeCompare(b.name));
}

/** Flat, indented options for a folder-picker <select> — "Root" first, then every folder
 * ordered depth-first so siblings/children stay visually grouped. */
export function buildFolderOptions(folders: Folder[]): { id: string | null; label: string; depth: number }[] {
  const options: { id: string | null; label: string; depth: number }[] = [{ id: null, label: 'Home (root)', depth: 0 }];

  function walk(parentId: string | null, depth: number) {
    for (const folder of getFolderChildren(folders, parentId)) {
      options.push({ id: folder.id, label: folder.name, depth });
      walk(folder.id, depth + 1);
    }
  }

  walk(null, 1);
  return options;
}

/** Every descendant folder id of rootFolderId (not including the root) — used client-side only
 * for excluding a folder (and its own subtree) from its own "move into" picker options; the
 * backend re-validates this independently regardless. */
export function getDescendantFolderIds(rootFolderId: string, folders: Folder[]): Set<string> {
  const result = new Set<string>();
  const frontier = [rootFolderId];

  while (frontier.length > 0) {
    const parentId = frontier.pop()!;
    for (const child of getFolderChildren(folders, parentId)) {
      if (!result.has(child.id)) {
        result.add(child.id);
        frontier.push(child.id);
      }
    }
  }

  return result;
}
