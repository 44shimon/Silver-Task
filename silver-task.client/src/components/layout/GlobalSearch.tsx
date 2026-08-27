import { useRef, useState, type FocusEvent, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Lock, Search, X } from 'lucide-react';
import { useTaskSearch } from '@/hooks/useTasks';
import { useDebouncedValue } from '@/hooks/useDebouncedValue';
import { STATUS_LABELS } from '@/types/task';
import './GlobalSearch.css';

const TASK_QUERY_PARAM = 'task';

/**
 * The Topbar search box — search-as-you-type across every project the caller can access,
 * backed by TaskService.SearchAsync (server-side, capped at 25 results) rather than a
 * client-side filter over preloaded data, since this has to reach tasks that were never
 * fetched into the browser at all. Clicking a result reuses the existing `?task=<id>`
 * convention (ProjectPage + TaskDetailPanel) instead of a separate results view.
 */
export function GlobalSearch() {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const debouncedQuery = useDebouncedValue(query, 300);
  const { data: results, isFetching } = useTaskSearch(debouncedQuery);
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement>(null);

  const showDropdown = isOpen && query.trim().length > 0;

  function openTask(taskId: string, projectId: string) {
    setIsOpen(false);
    setQuery('');
    navigate(`/projects/${projectId}?${TASK_QUERY_PARAM}=${taskId}`);
  }

  function handleBlur(event: FocusEvent<HTMLDivElement>) {
    if (!containerRef.current?.contains(event.relatedTarget as Node)) {
      setIsOpen(false);
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      setIsOpen(false);
      event.currentTarget.blur();
    }
  }

  return (
    <div className="global-search" ref={containerRef} onBlur={handleBlur}>
      <div className="topbar__search">
        <Search size={16} />
        <input
          type="text"
          placeholder="Search tasks..."
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setIsOpen(true);
          }}
          onFocus={() => setIsOpen(true)}
          onKeyDown={handleKeyDown}
        />
        {query && (
          <button type="button" className="global-search__clear" aria-label="Clear search" onClick={() => setQuery('')}>
            <X size={13} />
          </button>
        )}
      </div>

      {showDropdown && (
        <div className="global-search__dropdown">
          {isFetching && <div className="global-search__status">Searching...</div>}
          {!isFetching && results?.length === 0 && <div className="global-search__status">No tasks found.</div>}
          {!isFetching &&
            results?.map((task) => (
              <button
                key={task.id}
                type="button"
                className="global-search__result"
                onClick={() => openTask(task.id, task.projectId)}
              >
                <span className="global-search__result-title">
                  {task.blockedByCount > 0 && <Lock size={11} className="global-search__result-blocked-icon" aria-hidden="true" />}
                  {task.title}
                </span>
                <span className="global-search__result-meta">
                  {task.projectName ?? 'Unknown project'} · {STATUS_LABELS[task.status]}
                  {task.blockedByCount > 0 && ` · Blocked by ${task.blockedByCount}`}
                </span>
              </button>
            ))}
        </div>
      )}
    </div>
  );
}
