import { Calendar, GanttChart, GanttChartSquare, KanbanSquare, Repeat, Table } from 'lucide-react';
import './ProjectViewTabs.css';

export type ViewId = 'table' | 'kanban' | 'calendar' | 'timeline' | 'gantt' | 'recurring';

interface ProjectViewTabsProps {
  active: ViewId;
  onChange: (view: ViewId) => void;
}

// The first five views read the exact same `filteredTasks` the caller passes down (see
// ProjectPage) — switching tabs only changes the visualization, never the data, search/filter/
// sort state, or which task detail panel opens. The `enabled`-gating this had while views were
// still being built (Phases 17–20) is gone now that all five are live. "Recurring" (Phase 31) is
// the one exception — it shows recurrence *rules*, not task occurrences, so it has its own data
// source (see RecurringTasksView) rather than reusing filteredTasks.
const VIEWS: { id: ViewId; label: string; icon: typeof Table }[] = [
  { id: 'table', label: 'Table', icon: Table },
  { id: 'kanban', label: 'Kanban', icon: KanbanSquare },
  { id: 'calendar', label: 'Calendar', icon: Calendar },
  { id: 'timeline', label: 'Timeline', icon: GanttChart },
  { id: 'gantt', label: 'Gantt', icon: GanttChartSquare },
  { id: 'recurring', label: 'Recurring', icon: Repeat },
];

export function ProjectViewTabs({ active, onChange }: ProjectViewTabsProps) {
  return (
    <div className="view-tabs" role="tablist">
      {VIEWS.map((view) => {
        const Icon = view.icon;
        return (
          <button
            key={view.id}
            type="button"
            role="tab"
            aria-selected={view.id === active}
            className={`view-tabs__item${view.id === active ? ' view-tabs__item--active' : ''}`}
            onClick={() => onChange(view.id)}
          >
            <Icon size={14} />
            <span>{view.label}</span>
          </button>
        );
      })}
    </div>
  );
}
