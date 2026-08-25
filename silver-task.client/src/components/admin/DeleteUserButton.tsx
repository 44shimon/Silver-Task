import { Trash2 } from 'lucide-react';

interface DeleteUserButtonProps {
  userName: string;
  disabled?: boolean;
  onClick: () => void;
}

// Same "just a trigger" split as ResetPasswordButton — the actual confirmation flow lives in
// DeleteUserDialog, rendered at the page level rather than nested in a clipped table cell.
export function DeleteUserButton({ userName, disabled, onClick }: DeleteUserButtonProps) {
  return (
    <button
      type="button"
      className="icon-button admin-projects-table__delete"
      aria-label={`Delete ${userName}`}
      title={disabled ? "You can't delete your own account" : 'Delete user'}
      disabled={disabled}
      onClick={onClick}
    >
      <Trash2 size={14} />
    </button>
  );
}
