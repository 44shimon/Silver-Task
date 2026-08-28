import { useRef, useState, type FocusEvent, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Clock, FileText, FolderKanban, LayoutTemplate, MessageSquare, Search, Tag as TagIcon, User, X } from 'lucide-react';
import { useSearch } from '@/hooks/useSearch';
import { useDebouncedValue } from '@/hooks/useDebouncedValue';
import { useRecentSearches } from '@/hooks/useRecentSearches';
import { HighlightedText } from '@/components/search/HighlightedText';
import { SEARCH_ENTITY_LABELS, type SearchEntityType, type SearchResult } from '@/types/search';
import './GlobalSearch.css';

const TYPE_ICON: Record<SearchResult['type'], typeof Search> = {
  Task: Search,
  Project: FolderKanban,
  User: User,
  File: FileText,
  Comment: MessageSquare,
  Tag: TagIcon,
  Template: LayoutTemplate,
};

/**
 * The Topbar search box (spec #1/#3) — now backed by the unified GET /api/search endpoint
 * (Phase 42) instead of the Task-only TaskService.SearchAsync this used before, so the SAME
 * dropdown shows grouped results across every entity type the backend supports, rather than
 * maintaining two separate search implementations (CLAUDE.md's own "do not maintain two
 * unrelated implementations" rule). The full-page /search route (SearchResultsPage) is the
 * "View all results" destination for anything beyond this compact preview.
 */
export function GlobalSearch() {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const debouncedQuery = useDebouncedValue(query, 300);
  const { data, isFetching, isError } = useSearch(debouncedQuery, { pageSize: 20 });
  const { entries: recentSearches, record: recordSearch, clear: clearSearches } = useRecentSearches();
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement>(null);

  const trimmedQuery = query.trim();
  const showDropdown = isOpen;
  const tooShort = trimmedQuery.length > 0 && trimmedQuery.length < 2;

  const grouped = groupByType(data?.results ?? []);

  function openResult(result: SearchResult) {
    setIsOpen(false);
    setQuery('');
    recordSearch(trimmedQuery);
    navigate(result.actionUrl);
  }

  function goToFullResults(q: string) {
    const trimmed = q.trim();
    if (trimmed.length < 2) return;
    setIsOpen(false);
    setQuery('');
    recordSearch(trimmed);
    navigate(`/search?q=${encodeURIComponent(trimmed)}`);
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
    } else if (event.key === 'Enter') {
      goToFullResults(query);
    }
  }

  return (
    <div className="global-search" ref={containerRef} onBlur={handleBlur}>
      <div className="topbar__search">
        <Search size={16} aria-hidden="true" />
        <label htmlFor="global-search-input" className="global-search__visually-hidden">
          Search tasks, projects, files, and more
        </label>
        <input
          id="global-search-input"
          type="text"
          placeholder="Search tasks, projects, files... (Ctrl+K)"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setIsOpen(true);
          }}
          onFocus={() => setIsOpen(true)}
          onKeyDown={handleKeyDown}
          role="combobox"
          aria-expanded={showDropdown}
          aria-controls="global-search-listbox"
          aria-autocomplete="list"
          autoComplete="off"
        />
        {query && (
          <button type="button" className="global-search__clear" aria-label="Clear search" onClick={() => setQuery('')}>
            <X size={13} />
          </button>
        )}
      </div>

      {showDropdown && (
        <div className="global-search__dropdown" id="global-search-listbox" role="listbox" aria-label="Search results">
          {trimmedQuery.length === 0 && (
            <RecentSearchesPanel entries={recentSearches} onSelect={(q) => setQuery(q)} onClear={clearSearches} />
          )}

          {tooShort && <div className="global-search__status">Type at least 2 characters to search.</div>}

          {trimmedQuery.length >= 2 && (
            <>
              {isFetching && (
                <div className="global-search__status" aria-live="polite">
                  Searching...
                </div>
              )}
              {!isFetching && isError && (
                <div className="global-search__status global-search__status--error">
                  Unable to search right now. Please try again.
                </div>
              )}
              {!isFetching && !isError && data?.total === 0 && (
                <div className="global-search__status">No results found.</div>
              )}
              {!isFetching && !isError && grouped.length > 0 && (
                <>
                  {grouped.map(([type, items]) => (
                    <div key={type} className="global-search__group">
                      <div className="global-search__group-title">{SEARCH_ENTITY_LABELS[typeToFilterKey(type)]}</div>
                      {items.map((result) => {
                        const Icon = TYPE_ICON[result.type];
                        return (
                          <button
                            key={`${result.type}-${result.id}`}
                            type="button"
                            role="option"
                            aria-selected="false"
                            className="global-search__result"
                            onClick={() => openResult(result)}
                          >
                            <Icon size={13} className="global-search__result-icon" aria-hidden="true" />
                            <span className="global-search__result-body">
                              <span className="global-search__result-title">
                                <HighlightedText text={result.title} query={trimmedQuery} />
                              </span>
                              <span className="global-search__result-meta">
                                {result.snippet ? <HighlightedText text={result.snippet} query={trimmedQuery} /> : result.projectName}
                              </span>
                            </span>
                          </button>
                        );
                      })}
                    </div>
                  ))}
                  {data && data.total > data.results.length && (
                    <button type="button" className="global-search__view-all" onClick={() => goToFullResults(query)}>
                      View all {data.total} results
                    </button>
                  )}
                </>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

function RecentSearchesPanel({
  entries,
  onSelect,
  onClear,
}: {
  entries: { query: string; searchedAt: string }[];
  onSelect: (query: string) => void;
  onClear: () => void;
}) {
  if (entries.length === 0) {
    return null;
  }

  return (
    <div className="global-search__group">
      <div className="global-search__group-title global-search__group-title--recent">
        <span>
          <Clock size={12} aria-hidden="true" /> Recent Searches
        </span>
        <button type="button" onClick={onClear}>
          Clear
        </button>
      </div>
      {entries.map((entry) => (
        <button key={entry.query} type="button" className="global-search__recent-item" onClick={() => onSelect(entry.query)}>
          {entry.query}
        </button>
      ))}
    </div>
  );
}

function groupByType(results: SearchResult[]): [SearchResult['type'], SearchResult[]][] {
  const order: SearchResult['type'][] = ['Task', 'Project', 'File', 'User', 'Template', 'Tag', 'Comment'];
  const byType = new Map<SearchResult['type'], SearchResult[]>();
  for (const result of results) {
    const list = byType.get(result.type) ?? [];
    list.push(result);
    byType.set(result.type, list);
  }
  return order.filter((type) => byType.has(type)).map((type) => [type, byType.get(type)!.slice(0, 5)]);
}

function typeToFilterKey(type: SearchResult['type']): SearchEntityType {
  return type.toLowerCase() as SearchEntityType;
}
