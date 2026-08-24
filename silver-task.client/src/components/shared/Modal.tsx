import type { MouseEvent, ReactNode } from 'react';
import './Modal.css';

interface ModalProps {
  onClose: () => void;
  children: ReactNode;
}

// Shared centered-dialog shell (backdrop + box) — used by ResetPasswordDialog and
// UserNotFoundDialog rather than each defining its own near-identical overlay CSS. Clicking
// the backdrop itself closes it; clicking is checked by target === currentTarget so clicks
// inside the dialog content don't need their own stopPropagation handler.
export function Modal({ onClose, children }: ModalProps) {
  function handleBackdropClick(event: MouseEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget) {
      onClose();
    }
  }

  return (
    <div className="modal-backdrop" onClick={handleBackdropClick}>
      <div className="modal-dialog">{children}</div>
    </div>
  );
}
