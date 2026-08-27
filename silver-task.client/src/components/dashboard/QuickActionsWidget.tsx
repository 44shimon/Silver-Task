import { Link } from 'react-router-dom';
import { Calendar, FileText, ListChecks, Zap } from 'lucide-react';
import { useUserPreferences } from '@/hooks/useUserSettings';
import { NewTaskButton } from '@/components/spreadsheet/NewTaskButton';
import { DashboardWidget } from './DashboardWidget';
import './QuickActionsWidget.css';

// Task creation is inherently project-scoped in this app (there is no cross-project "new task"
// endpoint) — reuses UserPreference.DefaultProjectId (already exists, set on the Preferences
// page) to decide which project's *actual* NewTaskButton dialog to render here, rather than
// inventing a project-picker or a second creation flow. Project creation already has its own
// always-visible entry point (the Sidebar's own "+"), so it isn't duplicated here.
export function QuickActionsWidget() {
  const { data: preferences } = useUserPreferences();
  const defaultProjectId = preferences?.defaultProjectId;

  return (
    <DashboardWidget title="Quick Actions" icon={<Zap size={14} />}>
      <div className="quick-actions-widget">
        {defaultProjectId ? (
          <NewTaskButton projectId={defaultProjectId} />
        ) : (
          <span className="quick-actions-widget__hint" title="Set a default project in Settings → Preferences to enable quick task creation">
            New Task (set a default project)
          </span>
        )}
        {defaultProjectId && (
          <Link to={`/projects/${defaultProjectId}?view=calendar`} className="quick-actions-widget__link">
            <Calendar size={13} />
            Calendar
          </Link>
        )}
        <Link to="/my-tasks" className="quick-actions-widget__link">
          <ListChecks size={13} />
          My Tasks
        </Link>
        <Link to="/files/recent" className="quick-actions-widget__link">
          <FileText size={13} />
          Files
        </Link>
      </div>
    </DashboardWidget>
  );
}
