import { Search, X } from 'lucide-react';
import './Toolbar.css';

interface TaskSearchInputProps {
  value: string;
  onChange: (value: string) => void;
}

export function TaskSearchInput({ value, onChange }: TaskSearchInputProps) {
  return (
    <div className="task-search">
      <Search size={14} />
      <input type="text" placeholder="Search tasks..." value={value} onChange={(e) => onChange(e.target.value)} />
      {value && (
        <button type="button" className="task-search__clear" aria-label="Clear search" onClick={() => onChange('')}>
          <X size={13} />
        </button>
      )}
    </div>
  );
}
