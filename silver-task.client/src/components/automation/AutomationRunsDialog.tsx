import { useState } from 'react';
import { useAutomationRuns, useRetryAutomationRun } from '@/hooks/useAutomations';
import { TRIGGER_TYPE_LABELS } from '@/types/automation';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import './AutomationRunsDialog.css';

interface AutomationRunsDialogProps {
  automationId: string;
  automationName: string;
  canRetry: boolean;
  onClose: () => void;
}

export function AutomationRunsDialog({ automationId, automationName, canRetry, onClose }: AutomationRunsDialogProps) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useAutomationRuns(automationId, page);
  const retry = useRetryAutomationRun(automationId);
  const [retryError, setRetryError] = useState<string | null>(null);

  function handleRetry(runId: string) {
    setRetryError(null);
    retry.mutate(runId, {
      onError: (err) => setRetryError(err instanceof ApiError ? err.message : 'Could not retry this run.'),
    });
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Modal onClose={onClose} size="wide">
      <h2>Runs — {automationName}</h2>

      {isLoading && <p>Loading runs...</p>}
      {!isLoading && data?.items.length === 0 && <p className="automation-runs-dialog__empty">No runs yet.</p>}

      {!isLoading && data && data.items.length > 0 && (
        <table className="automation-runs-dialog__table">
          <thead>
            <tr>
              <th>Trigger</th>
              <th>Started</th>
              <th>Duration</th>
              <th>Status</th>
              <th>Result</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {data.items.map((run) => (
              <tr key={run.id}>
                <td>{TRIGGER_TYPE_LABELS[run.triggerType]}</td>
                <td>{new Date(run.startedAt).toLocaleString()}</td>
                <td>{run.durationMs !== null ? `${run.durationMs}ms` : '—'}</td>
                <td>
                  <span className={`automation-runs-dialog__status automation-runs-dialog__status--${run.status.toLowerCase()}`}>
                    {run.status}
                  </span>
                </td>
                <td className="automation-runs-dialog__result">{run.errorMessage ?? run.resultSummary ?? '—'}</td>
                <td>
                  {canRetry && run.status === 'Failed' && (
                    <button type="button" onClick={() => handleRetry(run.id)} disabled={retry.isPending}>
                      Retry
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {retryError && <p className="form-error">{retryError}</p>}

      {totalPages > 1 && (
        <div className="automation-runs-dialog__pager">
          <button type="button" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}>
            Previous
          </button>
          <span>
            Page {page} of {totalPages}
          </span>
          <button type="button" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}>
            Next
          </button>
        </div>
      )}
    </Modal>
  );
}
