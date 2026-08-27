import { Link } from 'react-router-dom';
import './StatCard.css';

interface StatCardProps {
  label: string;
  value: number;
  to?: string;
  active?: boolean;
  onClick?: () => void;
  tone?: 'default' | 'urgent';
}

// Extracted from the near-identical card markup already used twice (AdminDashboardPage,
// MyTasksSummary) — same {label, value} config-driven __value/__label convention, now a single
// shared component instead of a third independent copy. Existing usages are left untouched
// (no regression risk taken on two already-working screens for a purely cosmetic dedupe); this
// backs every new Phase 37 stat card instead.
export function StatCard({ label, value, to, active, onClick, tone = 'default' }: StatCardProps) {
  const className = `stat-card${active ? ' stat-card--active' : ''}${tone === 'urgent' && value > 0 ? ' stat-card--urgent' : ''}`;

  const content = (
    <>
      <span className="stat-card__value">{value}</span>
      <span className="stat-card__label">{label}</span>
    </>
  );

  if (to) {
    return (
      <Link to={to} className={className}>
        {content}
      </Link>
    );
  }

  return (
    <button type="button" className={className} onClick={onClick} disabled={!onClick}>
      {content}
    </button>
  );
}
