import { useEffect, type ReactNode } from 'react';
import { useUserPreferences } from '@/hooks/useUserSettings';
import { useNotificationHub } from '@/hooks/useNotifications';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const { data: preferences } = useUserPreferences();

  // Established once for the whole authenticated app (AppShell wraps every page via
  // RequireAuth), not per-page — a single persistent connection regardless of navigation.
  useNotificationHub();

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
    </div>
  );
}
