import type { ReactNode } from 'react';
import './BreakdownList.css';

interface BreakdownRow {
  key: string;
  count: number;
  badge: ReactNode;
}

interface BreakdownListProps {
  rows: BreakdownRow[];
}

// Shared bar-list rendering for Priority/Status breakdown widgets — same badge-driven row shape,
// just fed a different badge component (PriorityBadge/StatusBadge) and dataset by each caller.
export function BreakdownList({ rows }: BreakdownListProps) {
  const max = Math.max(1, ...rows.map((r) => r.count));

  return (
    <ul className="breakdown-list">
      {rows.map((row) => (
        <li key={row.key} className="breakdown-list__row">
          {row.badge}
          <div className="breakdown-list__bar">
            <div className="breakdown-list__bar-fill" style={{ width: `${(row.count / max) * 100}%` }} />
          </div>
          <span className="breakdown-list__count">{row.count}</span>
        </li>
      ))}
    </ul>
  );
}
