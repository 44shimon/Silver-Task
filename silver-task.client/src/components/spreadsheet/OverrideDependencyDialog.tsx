import { useState, type FormEvent } from 'react';
import { Modal } from '@/components/shared/Modal';
import '@/components/shared/ConfirmDeleteDialog.css';
import './OverrideDependencyDialog.css';

interface OverrideDependencyDialogProps {
  blockedBy: string[];
  isPending: boolean;
  errorMessage: string | null;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
}

// Only ever reachable from StatusDropdownCell after the backend has already rejected the plain
// status change as dependency-blocked (see DependencyBlockedException) AND the current viewer
// has Permissions.DependenciesOverride for this project — not every user can bypass a dependency,
// per the spec's own explicit requirement; that check happens again on the backend regardless
// (TaskService.EnsureNotBlockedByDependenciesAsync), this is only the UX gate.
export function OverrideDependencyDialog({ blockedBy, isPending, errorMessage, onConfirm, onCancel }: OverrideDependencyDialogProps) {
  const [reason, setReason] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = reason.trim();
    if (trimmed) {
      onConfirm(trimmed);
    }
  }

  return (
    <Modal onClose={onCancel}>
      <h2>Override Dependency</h2>
      <p className="confirm-delete-dialog__message">
        This task is blocked by: <strong>{blockedBy.join(', ')}</strong>. Overriding lets it proceed anyway — this is recorded in the
        task's activity history.
      </p>

      <form onSubmit={handleSubmit} className="override-dependency-dialog__form">
        <label className="override-dependency-dialog__field">
          <span>Reason</span>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="e.g. Emergency inspection approved by manager"
            rows={3}
            required
            autoFocus
          />
        </label>

        {errorMessage && <p className="form-error">{errorMessage}</p>}

        <div className="confirm-delete-dialog__actions">
          <button type="button" className="confirm-delete-dialog__cancel" onClick={onCancel} disabled={isPending}>
            Cancel
          </button>
          <button type="submit" className="confirm-delete-dialog__delete" disabled={isPending || !reason.trim()}>
            {isPending ? 'Overriding...' : 'Override'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
