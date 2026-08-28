import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { FileText, FolderKanban, LayoutTemplate, MessageSquare, Search, Tag as TagIcon, User } from 'lucide-react';
import { useSearch } from '@/hooks/useSearch';
import { useRecentSearches } from '@/hooks/useRecentSearches';
import { useActiveTags } from '@/hooks/useTags';
import { useProjects } from '@/hooks/useProjects';
import { HighlightedText } from '@/components/search/HighlightedText';
import {
  SEARCH_ENTITY_LABELS,
  SEARCH_ENTITY_TYPES,
  SEARCH_SORT_LABELS,
  SEARCH_SORT_OPTIONS,
  type SearchEntityType,
  type SearchResult,
  type SearchSort,
} from '@/types/search';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS, type TaskPriority, type TaskStatus } from '@/types/task';
import { formatDate, formatDateTime } from '@/utils/formatDate';
import './SearchResultsPage.css';

const TYPE_ICON: Record<SearchResult['type'], typeof Search> = {
  Task: Search,
  Project: FolderKanban,
  User: User,
  File: FileText,
  Comment: MessageSquare,
  Tag: TagIcon,
  Template: LayoutTemplate,
};

const Q_PARAM = 'q';
const TYPE_PARAM = 'type';
const PROJECT_PARAM = 'projectId';
const STATUS_PARAM = 'status';
const PRIORITY_PARAM = 'priority';
const TAG_PARAM = 'tagId';
const SORT_PARAM = 'sort';
const PAGE_PARAM = 'page';

/** Phase 42 — the full search results page (spec #21), URL-linkable and refresh/back-forward
 * safe: every piece of state (query, type tab, filters, sort, page) lives in the URL via
 * useSearchParams, the same idiom ProjectPage's own `?task=`/`?view=` params already established
 * (CLAUDE.md's own documented convention) — nothing here is kept in local-only state that a
 * refresh would lose. */
export function SearchResultsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { record: recordSearch } = useRecentSearches();
  const { data: projects } = useProjects();
  const { data: tags } = useActiveTags();

  const q = searchParams.get(Q_PARAM) ?? '';
  const [queryDraft, setQueryDraft] = useState(q);
  const type = (searchParams.get(TYPE_PARAM) as SearchEntityType | 'all' | null) ?? 'all';
  const projectId = searchParams.get(PROJECT_PARAM) ?? undefined;
  const status = (searchParams.get(STATUS_PARAM) as TaskStatus | null) ?? undefined;
  const priority = (searchParams.get(PRIORITY_PARAM) as TaskPriority | null) ?? undefined;
  const tagId = searchParams.get(TAG_PARAM) ?? undefined;
  const sort = (searchParams.get(SORT_PARAM) as SearchSort | null) ?? 'relevance';
  const page = Number(searchParams.get(PAGE_PARAM) ?? '1') || 1;
  const pageSize = 20;

  useEffect(() => {
    setQueryDraft(q);
  }, [q]);

  const { data, isLoading, isFetching, isError } = useSearch(q, {
    type,
    projectId,
    status,
    priority,
    tagId,
    sort,
    page,
    pageSize,
  });

  useEffect(() => {
    if (q.trim().length >= 2 && data) {
      recordSearch(q);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q, Boolean(data)]);

  function updateParams(mutate: (params: URLSearchParams) => void) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      mutate(next);
      return next;
    });
  }

  function submitQuery(event: FormEvent) {
    event.preventDefault();
    updateParams((params) => {
      params.set(Q_PARAM, queryDraft.trim());
      params.delete(PAGE_PARAM);
    });
  }

  function setType(next: SearchEntityType | 'all') {
    updateParams((params) => {
      if (next === 'all') params.delete(TYPE_PARAM);
      else params.set(TYPE_PARAM, next);
      params.delete(PAGE_PARAM);
    });
  }

  function setFilter(key: string, value: string | undefined) {
    updateParams((params) => {
      if (value) params.set(key, value);
      else params.delete(key);
      params.delete(PAGE_PARAM);
    });
  }

  function setSort(next: SearchSort) {
    updateParams((params) => {
      if (next === 'relevance') params.delete(SORT_PARAM);
      else params.set(SORT_PARAM, next);
    });
  }

  function goToPage(next: number) {
    updateParams((params) => params.set(PAGE_PARAM, String(next)));
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;
  const counts = data?.counts;

  return (
    <div className="search-results-page">
      <form className="search-results-page__search-bar" onSubmit={submitQuery}>
        <Search size={16} aria-hidden="true" />
        <label htmlFor="search-page-input" className="search-results-page__visually-hidden">
          Search
        </label>
        <input
          id="search-page-input"
          type="text"
          value={queryDraft}
          onChange={(e) => setQueryDraft(e.target.value)}
          placeholder="Search tasks, projects, files..."
        />
        <button type="submit">Search</button>
      </form>

      <h1>
        Search Results{q && <span className="search-results-page__query"> for "{q}"</span>}
      </h1>

      <nav className="search-results-page__tabs" aria-label="Filter by result type">
        <button type="button" className={type === 'all' ? 'active' : ''} onClick={() => setType('all')}>
          All {data && `(${data.total})`}
        </button>
        {SEARCH_ENTITY_TYPES.map((t) => (
          <button key={t} type="button" className={type === t ? 'active' : ''} onClick={() => setType(t)}>
            {SEARCH_ENTITY_LABELS[t]} {counts && `(${countFor(counts, t)})`}
          </button>
        ))}
      </nav>

      <div className="search-results-page__toolbar">
        <div className="search-results-page__filters">
          <select value={projectId ?? ''} onChange={(e) => setFilter(PROJECT_PARAM, e.target.value || undefined)}>
            <option value="">All Projects</option>
            {projects?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          {(type === 'all' || type === 'task') && (
            <>
              <select value={status ?? ''} onChange={(e) => setFilter(STATUS_PARAM, e.target.value || undefined)}>
                <option value="">Any Status</option>
                {STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>
                    {STATUS_LABELS[s]}
                  </option>
                ))}
              </select>
              <select value={priority ?? ''} onChange={(e) => setFilter(PRIORITY_PARAM, e.target.value || undefined)}>
                <option value="">Any Priority</option>
                {PRIORITY_OPTIONS.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </>
          )}
          <select value={tagId ?? ''} onChange={(e) => setFilter(TAG_PARAM, e.target.value || undefined)}>
            <option value="">Any Tag</option>
            {tags?.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </select>
        </div>

        <label className="search-results-page__sort">
          Sort by
          <select value={sort} onChange={(e) => setSort(e.target.value as SearchSort)}>
            {SEARCH_SORT_OPTIONS.map((s) => (
              <option key={s} value={s}>
                {SEARCH_SORT_LABELS[s]}
              </option>
            ))}
          </select>
        </label>
      </div>

      {q.trim().length < 2 && (
        <p className="search-results-page__hint">Type at least 2 characters to search.</p>
      )}

      {q.trim().length >= 2 && (isLoading || isFetching) && <p className="search-results-page__status">Searching...</p>}

      {q.trim().length >= 2 && !isFetching && isError && (
        <p className="search-results-page__status search-results-page__status--error">
          Unable to search right now. Please try again.
        </p>
      )}

      {q.trim().length >= 2 && !isFetching && !isError && data && data.results.length === 0 && (
        <div className="search-results-page__empty">
          <p>No results found.</p>
          <p>Try:</p>
          <ul>
            <li>Checking your spelling</li>
            <li>Using fewer words</li>
            <li>Searching by task/project name</li>
          </ul>
        </div>
      )}

      {data && data.results.length > 0 && (
        <ul className="search-results-page__list">
          {data.results.map((result) => (
            <ResultCard key={`${result.type}-${result.id}`} result={result} query={q} onOpen={() => navigate(result.actionUrl)} />
          ))}
        </ul>
      )}

      {data && totalPages > 1 && (
        <div className="search-results-page__pagination">
          <button type="button" disabled={page <= 1} onClick={() => goToPage(page - 1)}>
            Previous
          </button>
          <span>
            Page {page} of {totalPages}
          </span>
          <button type="button" disabled={page >= totalPages} onClick={() => goToPage(page + 1)}>
            Next
          </button>
        </div>
      )}
    </div>
  );
}

function countFor(counts: NonNullable<ReturnType<typeof useSearch>['data']>['counts'], type: SearchEntityType): number {
  switch (type) {
    case 'task': return counts.tasks;
    case 'project': return counts.projects;
    case 'user': return counts.users;
    case 'file': return counts.files;
    case 'comment': return counts.comments;
    case 'tag': return counts.tags;
    case 'template': return counts.templates;
  }
}

function ResultCard({ result, query, onOpen }: { result: SearchResult; query: string; onOpen: () => void }) {
  const Icon = TYPE_ICON[result.type];

  return (
    <li className="search-result-card">
      <button type="button" className="search-result-card__button" onClick={onOpen}>
        <Icon size={16} className="search-result-card__icon" aria-hidden="true" />
        <div className="search-result-card__body">
          <div className="search-result-card__title-row">
            <span className="search-result-card__title">
              <HighlightedText text={result.title} query={query} />
            </span>
            <span className="search-result-card__type-badge">{result.type}</span>
          </div>
          {result.snippet && (
            <p className="search-result-card__snippet">
              <HighlightedText text={result.snippet} query={query} />
            </p>
          )}
          <div className="search-result-card__meta">
            {result.projectName && <span>Project: {result.projectName}</span>}
            {result.assigneeName && <span>Assignee: {result.assigneeName}</span>}
            {result.status && <span>Status: {result.status}</span>}
            {result.priority && <span>Priority: {result.priority}</span>}
            {result.dueDate && <span>Due: {formatDate(result.dueDate)}</span>}
            {result.tagNames && result.tagNames.length > 0 && <span>{result.tagNames.join(', ')}</span>}
            <span className="search-result-card__updated">Updated {formatDateTime(result.updatedAt)}</span>
          </div>
        </div>
      </button>
    </li>
  );
}
