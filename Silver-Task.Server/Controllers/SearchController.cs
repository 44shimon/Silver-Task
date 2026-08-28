using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Exceptions;
using Silver_Task.Server.Models.DTOs.Search;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Phase 42 — GET /api/search, following the same query-parameter convention every
    /// other list endpoint in this app already uses (see ReportsController's own filter
    /// parameters). Authorization is entirely inside ISearchService itself: every entity query
    /// there is pre-scoped to what the caller can already see (own/member projects, Administrator-
    /// only user search, etc.) — there is no "load everything then filter" step anywhere, so this
    /// controller has nothing further to gate beyond authentication (the global FallbackPolicy
    /// already requires that).</summary>
    [ApiController]
    [Route("api/search")]
    public class SearchController(ISearchService searchService) : ControllerBase
    {
        private readonly ISearchService _searchService = searchService;

        [HttpGet]
        public async Task<ActionResult<SearchResponseDto>> Search(
            [FromQuery] string q,
            [FromQuery] string? type,
            [FromQuery] Guid? projectId,
            [FromQuery] TaskItemStatus? status,
            [FromQuery] TaskPriority? priority,
            [FromQuery] Guid? assigneeId,
            [FromQuery] Guid? tagId,
            [FromQuery] DateOnly? dateFrom,
            [FromQuery] DateOnly? dateTo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? sort = null)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                throw new ValidationException("A search query is required.");
            }
            // A generous upper bound, not a hard business rule — protects the backend from a
            // single pathological request rather than any real user need (spec #90's own
            // "fail safely rather than consuming unlimited resources").
            if (q.Length > 200)
            {
                throw new ValidationException("Search query is too long.");
            }

            var request = new SearchRequest
            {
                Query = q,
                Type = type,
                ProjectId = projectId,
                Status = status,
                Priority = priority,
                AssigneeId = assigneeId,
                TagId = tagId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Page = page,
                PageSize = pageSize,
                Sort = sort
            };

            return Ok(await _searchService.SearchAsync(request, User.GetUserId(), User.GetRole()));
        }
    }
}
