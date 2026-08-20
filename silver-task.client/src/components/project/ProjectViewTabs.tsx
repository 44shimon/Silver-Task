import './ProjectViewTabs.css';

type ViewId = 'table' | 'kanban' | 'calendar' | 'timeline' | 'gantt';

interface ProjectViewTabsProps {
  active: ViewId;
}

// Only "table" is implemented (this phase). The others are intentionally visible-but-disabled:
// the view system is designed so new views can be added without reworking this component or
// the page around it, per the project's view architecture.
const VIEWS: { id: ViewId; label: string; enabled: boolean }[] = [
  { id: 'table', label: 'Table', enabled: true },
  { id: 'kanban', label: 'Kanban', enabled: false },
  { id: 'calendar', label: 'Calendar', enabled: false },
  { id: 'timeline', label: 'Timeline', enabled: false },
  { id: 'gantt', label: 'Gantt', enabled: false },
];

export function ProjectViewTabs({ active }: ProjectViewTabsProps) {
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
        >
          {view.label}
        </button>
      ))}
    </div>
  );
}
