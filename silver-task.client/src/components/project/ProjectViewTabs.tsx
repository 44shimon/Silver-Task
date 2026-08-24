import './ProjectViewTabs.css';

export type ViewId = 'table' | 'kanban' | 'calendar' | 'timeline' | 'gantt';

interface ProjectViewTabsProps {
  active: ViewId;
  onChange: (view: ViewId) => void;
}

// Table/Kanban/Calendar are implemented; Timeline/Gantt stay visible-but-disabled until their
// phases land — the view system is designed so new views can be added without reworking this
// component or the page around it, per the project's view architecture.
const VIEWS: { id: ViewId; label: string; enabled: boolean }[] = [
  { id: 'table', label: 'Table', enabled: true },
  { id: 'kanban', label: 'Kanban', enabled: true },
  { id: 'calendar', label: 'Calendar', enabled: true },
  { id: 'timeline', label: 'Timeline', enabled: false },
  { id: 'gantt', label: 'Gantt', enabled: false },
];

export function ProjectViewTabs({ active, onChange }: ProjectViewTabsProps) {
  return (
    <div className="view-tabs" role="tablist">
      {VIEWS.map((view) => (
        <button
          key={view.id}
          type="button"
          role="tab"
          aria-selected={view.id === active}
          className={`view-tabs__item${view.id === active ? ' view-tabs__item--active' : ''}`}
          disabled={!view.enabled}
          title={view.enabled ? undefined : 'Coming soon'}
          onClick={() => onChange(view.id)}
        >
          {view.label}
        </button>
      ))}
    </div>
  );
}
