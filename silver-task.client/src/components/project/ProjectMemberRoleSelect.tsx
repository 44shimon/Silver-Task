import type { ChangeEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import type { ProjectMember, ProjectRole } from '@/types/project';
import { useSetProjectMemberRole } from '@/hooks/useProjects';
import '@/components/spreadsheet/DropdownCell.css';
import './ProjectMemberRoleSelect.css';

const ROLE_OPTIONS: ProjectRole[] = ['Manager', 'Member', 'Viewer'];

interface ProjectMemberRoleSelectProps {
  projectId: string;
  member: ProjectMember;
}

/** Per-project role picker (Phase 32) — same interaction pattern as the Admin Users table's
 * UserRoleDropdownCell, but for ProjectMember.role rather than the system-wide UserRole. Only
 * ever rendered for a caller who already has Projects.ManageMembers (see ProjectPage). */
export function ProjectMemberRoleSelect({ projectId, member }: ProjectMemberRoleSelectProps) {
  const setRole = useSetProjectMemberRole(projectId);

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const role = event.target.value as ProjectRole;
    if (role !== member.role) {
      setRole.mutate({ userId: member.user.id, role });
    }
  }

  return (
    <div className="dropdown-cell-wrapper">
      <select
        className={`dropdown-cell dropdown-cell--badge project-role-badge--${member.role.toLowerCase()}${setRole.isError ? ' dropdown-cell--error' : ''}`}
        value={member.role}
        onChange={handleChange}
        disabled={setRole.isPending}
        title={setRole.isError ? 'Could not save — try again' : undefined}
      >
        {ROLE_OPTIONS.map((role) => (
          <option key={role} value={role}>
            {role}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}
