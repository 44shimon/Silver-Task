import './ProjectViewTabs.css';

export type ViewId = 'table' | 'kanban' | 'calendar' | 'timeline' | 'gantt';

interface ProjectViewTabsProps {
  active: ViewId;
  onChange: (view: ViewId) => void;
}

// All five views are implemented — the view system was designed from Phase 17 onward so each
// new one could be added without reworking this component or the page around it.
const VIEWS: { id: ViewId; label: string; enabled: boolean }[] = [
  { id: 'table', label: 'Table', enabled: true },
  { id: 'kanban', label: 'Kanban', enabled: true },
  { id: 'calendar', label: 'Calendar', enabled: true },
  { id: 'timeline', label: 'Timeline', enabled: true },
  { id: 'gantt', label: 'Gantt', enabled: true },
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
