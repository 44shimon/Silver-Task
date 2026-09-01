import { useEffect, type ReactNode } from 'react';
import { useUserPreferences } from '@/hooks/useUserSettings';
import { useNotificationHub } from '@/hooks/useNotifications';
import { useLastVisitedPage } from '@/hooks/useLastVisitedPage';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';
import { VersionFooter } from './VersionFooter';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const { data: preferences } = useUserPreferences();

  // Established once for the whole authenticated app (AppShell wraps every page via
  // RequireAuth), not per-page — a single persistent connection regardless of navigation.
  useNotificationHub();
  useLastVisitedPage();

  // Phase 42 — Ctrl+K (Cmd+K on Mac) focuses the global search box. No keyboard-shortcut system
  // existed anywhere in this app before (confirmed by research), so this is a fresh, minimal
  // addition scoped to exactly the one shortcut the spec asks for — not a general command-palette
  // framework. Registered once for the whole authenticated app, same as useNotificationHub above.
  useEffect(() => {
    function handleKeyDown(event: globalThis.KeyboardEvent) {
      const isShortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k';
      if (!isShortcut) {
        return;
      }
      event.preventDefault();
      document.getElementById('global-search-input')?.focus();
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, []);

  // System (the default) leaves the attribute unset entirely, so index.css's
  // prefers-color-scheme media query keeps governing — Light/Dark force an explicit override
  // regardless of OS preference. Applied here (not per-page) so it's in effect everywhere
  // immediately after login, not just once Preferences has been visited.
  useEffect(() => {
    if (preferences?.theme === 'Light' || preferences?.theme === 'Dark') {
      document.documentElement.dataset.theme = preferences.theme.toLowerCase();
    } else {
      delete document.documentElement.dataset.theme;
    }
  }, [preferences?.theme]);

  return (
    <div className="app-shell">
      <Topbar />
      <div className="app-shell__body">
        <Sidebar />
        <main className="app-shell__content">{children}</main>
      </div>
      <VersionFooter />
    </div>
  );
}
