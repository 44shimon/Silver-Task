import { Modal } from './Modal';
import './ConfirmDeleteDialog.css';

interface ConfirmDeleteDialogProps {
  title: string;
  /** The backend's 409 message, already human-readable (e.g. "'X' has values on 5 tasks..."). */
  message: string;
  /** Shown when the caller has a non-destructive alternative (e.g. deactivate) — omit otherwise. */
  onDeactivate?: () => void;
  onConfirmDelete: () => void;
  onClose: () => void;
  isDeleting?: boolean;
}

// Shown when a delete is attempted on a custom field/option that already has task values —
// the backend rejects that first attempt with a 409 explaining how many tasks are affected;
// this surfaces that message and requires an explicit second confirmation before retrying with
// confirm=true, per the "do not silently destroy task data" requirement.
export function ConfirmDeleteDialog({
  title,
  message,
  onDeactivate,
  onConfirmDelete,
  onClose,
  isDeleting,
}: ConfirmDeleteDialogProps) {
  return (
    <Modal onClose={onClose}>
      <h2>{title}</h2>
      <p className="confirm-delete-dialog__message">{message}</p>
      <div className="confirm-delete-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={isDeleting}>
          Cancel
        </button>
        {onDeactivate && (
          <button type="button" className="confirm-delete-dialog__deactivate" onClick={onDeactivate} disabled={isDeleting}>
            Deactivate instead
          </button>
        )}
        <button type="button" className="confirm-delete-dialog__delete" onClick={onConfirmDelete} disabled={isDeleting}>
          {isDeleting ? 'Deleting...' : 'Delete permanently'}
        </button>
      </div>
    </Modal>
  );
}
