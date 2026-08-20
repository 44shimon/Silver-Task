import type { ReactNode } from 'react';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
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
