import type { MouseEvent, ReactNode } from 'react';
import './Modal.css';

interface ModalProps {
  onClose: () => void;
  children: ReactNode;
  /** Default (undefined) is the standard 340px dialog every other modal uses. "wide" is for
   * content-heavy forms (e.g. CustomFieldFormModal) that need more room — widening the dialog
   * itself here, rather than a child forcing its own width past a fixed-width parent, which
   * just overflows the rounded box instead of actually growing it. */
  size?: 'wide';
}

// Shared centered-dialog shell (backdrop + box) — used by ResetPasswordDialog and
// UserNotFoundDialog rather than each defining its own near-identical overlay CSS. Clicking
// the backdrop itself closes it; clicking is checked by target === currentTarget so clicks
// inside the dialog content don't need their own stopPropagation handler.
export function Modal({ onClose, children, size }: ModalProps) {
  function handleBackdropClick(event: MouseEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget) {
      onClose();
    }
  }

  return (
    <div className="modal-backdrop" onClick={handleBackdropClick}>
      <div className={`modal-dialog${size === 'wide' ? ' modal-dialog--wide' : ''}`}>{children}</div>
    </div>
  );
}
