namespace Silver_Task.Server.Common
{
    /// <summary>Phase 61 — the shared paging/sorting convention for the public v1 API
    /// (Controllers/V1/*). Applied in-controller, over the same fully-authorized in-memory list
    /// the existing internal endpoints already load via IProjectService/ITaskService (the
    /// underlying services are untouched — see docs/public-api.md for why server-side paging
    /// lives here rather than pushed into those services: a non-browser API client has no
    /// client-side filtering option the way the SPA does, but that's a v1-controller-layer
    /// concern, not a reason to change the internal architecture Phase 60 already validated).</summary>
    public static class ApiV1QueryOptions
    {
        public const int DefaultPageSize = 25;
        public const int MaxPageSize = 100;

        /// <summary>Clamps page to >= 1 and pageSize to [1, MaxPageSize] — never throws on an
        /// out-of-range value, since a client requesting page=0 or pageSize=10000 almost always
        /// means "give me a sane default/ceiling", not a request that should fail outright.</summary>
        public static (int Page, int PageSize) ParsePaging(int page, int pageSize)
        {
            var clampedPage = Math.Max(1, page);
            var clampedPageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
            return (clampedPage, clampedPageSize);
        }

        /// <summary>Single-field sort: a bare field name for ascending, a "-" prefix for
        /// descending (e.g. "-createdAt") — one canonical convention for every v1 list endpoint,
        /// rather than the internal API's several different ad-hoc shapes (SearchController's
        /// single "sort" string, ProjectsController.GetFiles's separate sortField/sortDescending
        /// pair). <paramref name="sortSelectors"/> maps a lowercase field name to a key selector;
        /// an unrecognized or omitted field name returns the sequence unchanged (never a 400 —
        /// sorting is a refinement, not a required, validatable input).</summary>
        public static IOrderedEnumerable<T>? ApplySort<T>(
            IEnumerable<T> source,
            string? sort,
            IReadOnlyDictionary<string, Func<T, IComparable?>> sortSelectors)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return null;
            }

            var descending = sort.StartsWith('-');
            var field = (descending ? sort[1..] : sort).Trim().ToLowerInvariant();
            if (!sortSelectors.TryGetValue(field, out var selector))
            {
                return null;
            }

            return descending
                ? source.OrderByDescending(selector)
                : source.OrderBy(selector);
        }
    }
}
