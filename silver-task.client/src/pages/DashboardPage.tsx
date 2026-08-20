import { useHealthCheck } from '@/hooks/useHealthCheck';

export function DashboardPage() {
  const { data, isLoading, isError, error } = useHealthCheck();

  return (
    <div className="dashboard">
      <h1>Welcome to Silver-Task</h1>
      <p>Select or create a project to get started.</p>

      <div className="status-card">
        <span className="status-card__label">API connection:</span>
        {isLoading && <span className="status-card__value status-card__value--pending">Checking...</span>}
        {isError && (
          <span className="status-card__value status-card__value--error">
            Unreachable{error instanceof Error ? ` (${error.message})` : ''}
          </span>
        )}
        {data && (
          <span className="status-card__value status-card__value--ok">
            Connected &mdash; server time {new Date(data.timeUtc).toLocaleTimeString()}
          </span>
        )}
      </div>
    </div>
  );
}
