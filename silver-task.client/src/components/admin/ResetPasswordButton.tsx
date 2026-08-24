import { KeyRound } from 'lucide-react';

interface ResetPasswordButtonProps {
  userName: string;
  onClick: () => void;
}

// Deliberately just a trigger — the actual form lives in ResetPasswordDialog, rendered at the
// page level (see AdminUsersPage) rather than nested here. A <details> popover anchored inside
// a table cell gets visually clipped by that cell's `overflow: hidden` (needed elsewhere for
// text-ellipsis truncation), so the button would appear to do nothing when clicked.
export function ResetPasswordButton({ userName, onClick }: ResetPasswordButtonProps) {
  return (
    <button type="button" className="icon-button" aria-label={`Reset password for ${userName}`} title="Reset password" onClick={onClick}>
      <KeyRound size={14} />
    </button>
  );
}
