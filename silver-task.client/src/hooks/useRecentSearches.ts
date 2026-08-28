import { useState } from 'react';

const STORAGE_KEY = 'silvertask:recentSearches';
const MAX_ENTRIES = 8;

export interface RecentSearchEntry {
  query: string;
  searchedAt: string;
}

// Client-side localStorage tracking, mirroring useRecentReports/useLastVisitedPage's own
// established precedent exactly — not a new backend history table (spec #52's own "optionally
// maintain recent searches" + CLAUDE.md's "reuse existing recent-item infrastructure"). Recording
// is explicit (call recordRecentSearch when a search is actually submitted/navigated to), never
// on every keystroke (spec #76's own "do not create an audit entry for every keystroke").
export function recordRecentSearch(query: string): void {
  const trimmed = query.trim();
  if (!trimmed) {
    return;
  }
  const existing = getRecentSearches().filter((e) => e.query.toLowerCase() !== trimmed.toLowerCase());
  const next = [{ query: trimmed, searchedAt: new Date().toISOString() }, ...existing].slice(0, MAX_ENTRIES);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
}

export function getRecentSearches(): RecentSearchEntry[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as RecentSearchEntry[]) : [];
  } catch {
    return [];
  }
}

export function clearRecentSearches(): void {
  localStorage.removeItem(STORAGE_KEY);
}

/** A small reactive wrapper around the plain functions above, for components that need to
 * re-render immediately after recording/clearing (localStorage itself has no change event within
 * the same tab). */
export function useRecentSearches() {
  const [entries, setEntries] = useState<RecentSearchEntry[]>(getRecentSearches);

  function record(query: string) {
    recordRecentSearch(query);
    setEntries(getRecentSearches());
  }

  function clear() {
    clearRecentSearches();
    setEntries([]);
  }

  return { entries, record, clear };
}
