import { useEffect } from 'react';

const STORAGE_KEY = 'silvertask:recentReports';
const MAX_ENTRIES = 5;

export interface RecentReportEntry {
  path: string;
  label: string;
  visitedAt: string;
}

// Client-side localStorage tracking, mirroring useLastVisitedPage's own precedent exactly — not
// a new backend history system, per the spec's own "do not create an unrelated history system"
// instruction. "Recent" here means recently-visited report TABS, distinct from Saved Reports
// (My Reports) — a tab can be recent without ever being saved.
export function useRecentReports(activeTab: string, label: string) {
  useEffect(() => {
    const path = `/reports/${activeTab}`;
    const existing = getRecentReports().filter((e) => e.path !== path);
    const next = [{ path, label, visitedAt: new Date().toISOString() }, ...existing].slice(0, MAX_ENTRIES);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  }, [activeTab, label]);
}

export function getRecentReports(): RecentReportEntry[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as RecentReportEntry[]) : [];
  } catch {
    return [];
  }
}
