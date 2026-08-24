import { Link } from 'react-router-dom';
import { Modal } from '@/components/shared/Modal';
import './UserNotFoundDialog.css';

interface UserNotFoundDialogProps {
  email: string;
  /** Only Administrators can create accounts (see Admin → Users), so only they get the link —
   * everyone else just sees the plain explanation. */
  isAdmin: boolean;
  onClose: () => void;
}

export function UserNotFoundDialog({ email, isAdmin, onClose }: UserNotFoundDialogProps) {
  return (
    <Modal onClose={onClose}>
      <h2>User not found</h2>
      <p className="user-not-found-dialog__message">
        No Silver-Task account exists for <strong>{email}</strong>.
      </p>

      {isAdmin ? (
        <p className="user-not-found-dialog__message">
          Create an account for them on the Admin Users page, then come back here and add them by email.
        </p>
      ) : (
        <p className="user-not-found-dialog__message">Ask an Administrator to create an account for them first.</p>
      )}

      <div className="user-not-found-dialog__actions">
        {isAdmin && (
          <Link to="/admin/users" className="user-not-found-dialog__link" onClick={onClose}>
            Go to Admin → Users
          </Link>
        )}
        <button type="button" className="user-not-found-dialog__close" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  );
}
