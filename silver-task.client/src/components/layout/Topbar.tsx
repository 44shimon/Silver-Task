import { usePublicSettings } from '@/hooks/useSystemSettings';
import { GlobalSearch } from './GlobalSearch';
import { UserMenu } from './UserMenu';
import { NotificationBell } from './NotificationBell';

export function Topbar() {
  const { data: publicSettings } = usePublicSettings();

  return (
    <header className="topbar">
      <div className="topbar__brand">{publicSettings?.applicationName ?? 'Silver-Task'}</div>
      <GlobalSearch />
      <div className="topbar__actions">
        <NotificationBell />
        <UserMenu />
      </div>
    </header>
  );
}
