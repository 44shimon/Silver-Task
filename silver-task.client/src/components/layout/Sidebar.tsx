import { LayoutGrid } from 'lucide-react';

export function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="sidebar__section-title">Projects</div>
      <div className="sidebar__empty">
        <LayoutGrid size={16} />
        <span>No projects yet</span>
      </div>
    </aside>
  );
}
