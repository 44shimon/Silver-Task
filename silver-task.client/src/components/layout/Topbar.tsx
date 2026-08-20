import { Bell, Search, User } from 'lucide-react';

export function Topbar() {
  return (
    <header className="topbar">
      <div className="topbar__brand">Silver-Task</div>
      <div className="topbar__search">
        <Search size={16} />
        <input type="text" placeholder="Search tasks..." disabled />
      </div>
      <div className="topbar__actions">
        <button className="icon-button" type="button" aria-label="Notifications" disabled>
          <Bell size={18} />
        </button>
        <button className="icon-button" type="button" aria-label="Account" disabled>
          <User size={18} />
        </button>
      </div>
    </header>
  );
}
