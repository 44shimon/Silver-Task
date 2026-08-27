import './DashboardGreeting.css';

interface DashboardGreetingProps {
  name: string;
}

function greetingWord(hour: number): string {
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

// Browser-local time for the greeting word/date display only (a "good morning" that's off by an
// hour or two is harmless) — the actual due/overdue/week-boundary math (DashboardService) uses
// the user's configured UserPreference.TimeZone, not this.
export function DashboardGreeting({ name }: DashboardGreetingProps) {
  const now = new Date();
  return (
    <div className="dashboard-greeting">
      <h1>
        {greetingWord(now.getHours())}, {name}
      </h1>
      <p>{now.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}</p>
    </div>
  );
}
