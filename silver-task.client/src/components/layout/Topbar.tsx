import { Bell } from 'lucide-react';
import { usePublicSettings } from '@/hooks/useSystemSettings';
import { GlobalSearch } from './GlobalSearch';
import { UserMenu } from './UserMenu';

export function Topbar() {
  const { data: publicSettings } = usePublicSettings();

  return (
    <header className="topbar">
      <div className="topbar__brand">{publicSettings?.applicationName ?? 'Silver-Task'}</div>
      <GlobalSearch />
      <div className="topbar__actions">
        <button className="icon-button" type="button" aria-label="Notifications" disabled>
          <Bell size={18} />
        </button>
        <UserMenu />
      </div>
    </header>
  );
}
