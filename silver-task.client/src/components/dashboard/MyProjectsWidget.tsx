import { Link } from 'react-router-dom';
import { LayoutGrid } from 'lucide-react';
import type { ProjectProgress } from '@/types/dashboard';
import { DashboardWidget } from './DashboardWidget';
import './MyProjectsWidget.css';

interface MyProjectsWidgetProps {
  projects: ProjectProgress[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function MyProjectsWidget({ projects, isLoading, isError, onRetry }: MyProjectsWidgetProps) {
  return (
    <DashboardWidget
      title="My Projects"
      icon={<LayoutGrid size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      isEmpty={projects.length === 0}
      emptyTitle="No projects yet"
    >
      <ul className="my-projects-widget">
        {projects.map((project) => (
          <li key={project.projectId}>
            <Link to={`/projects/${project.projectId}`} className="my-projects-widget__row">
              <div className="my-projects-widget__header">
                <span className="my-projects-widget__name">{project.projectName}</span>
                <span className="my-projects-widget__percent">{project.percentComplete}%</span>
              </div>
              <div className="my-projects-widget__bar">
                <div className="my-projects-widget__bar-fill" style={{ width: `${project.percentComplete}%` }} />
              </div>
              <div className="my-projects-widget__counts">
                <span>Completed: {project.completedCount}</span>
                <span>Remaining: {project.openCount}</span>
              </div>
            </Link>
          </li>
        ))}
      </ul>
    </DashboardWidget>
  );
}
