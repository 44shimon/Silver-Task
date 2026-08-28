import { useQuery } from '@tanstack/react-query';
import { searchApi } from '@/api/searchApi';
import type { SearchFilters } from '@/types/search';

const MIN_QUERY_LENGTH = 2;

/** Backs both the Topbar's quick dropdown and the full /search results page — the single search
 * implementation both funnel through (CLAUDE.md's own "do not maintain two unrelated
 * implementations" rule), just with different `filters` (a small pageSize for the dropdown, full
 * pagination/filters for the page). Server-side minimum-length handling already exists
 * (SearchController just returns an empty result for a too-short query), but `enabled` here
 * avoids even firing that request. */
export function useSearch(query: string, filters: SearchFilters = {}) {
  const trimmed = query.trim();
  return useQuery({
    queryKey: ['search', trimmed, filters],
    queryFn: () => searchApi.search(trimmed, filters),
    enabled: trimmed.length >= MIN_QUERY_LENGTH,
  });
}

export { MIN_QUERY_LENGTH };
