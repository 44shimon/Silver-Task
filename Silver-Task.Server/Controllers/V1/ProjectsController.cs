using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.Common;
using Silver_Task.Server.Models.DTOs.Projects;
using Silver_Task.Server.Models.DTOs.V1;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers.V1
{
    /// <summary>Phase 61 — one of two reference resources for the public v1 API foundation (the
    /// other is Controllers/V1/TasksController). Delegates every operation to the existing,
    /// unmodified IProjectService — no new business logic or authorization here; the same
    /// ProjectAccessService tiers the internal ProjectsController relies on apply identically.
    /// Own request/response DTOs (Models/DTOs/V1/ProjectV1Dto.cs) keep the v1 contract decoupled
    /// from whatever the internal ProjectDto does next for SPA-only reasons. See
    /// docs/public-api.md for the full conventions this establishes.
    ///
    /// Phase 62 — [Authorize(Policy = "ApiKeyOrCookie")] accepts either the SPA's existing cookie
    /// session or an X-Api-Key header (see ApiKeyAuthenticationHandler), replacing the implicit
    /// reliance on the cookie-only global FallbackPolicy every other controller still uses.</summary>
    [ApiController]
    [Route("api/v1/projects")]
    [Authorize(Policy = "ApiKeyOrCookie")]
    public class ProjectsController(IProjectService projectService) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;

        private static readonly IReadOnlyDictionary<string, Func<Project, IComparable?>> SortSelectors =
            new Dictionary<string, Func<Project, IComparable?>>
            {
                ["name"] = p => p.Name,
                ["createdat"] = p => p.CreatedAt,
                ["updatedat"] = p => p.UpdatedAt,
            };

        [HttpGet]
        public async Task<ActionResult<PagedResult<ProjectV1Dto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = ApiV1QueryOptions.DefaultPageSize,
            [FromQuery] bool includeArchived = false,
            [FromQuery] string? q = null,
            [FromQuery] string? sort = null)
        {
            var (clampedPage, clampedPageSize) = ApiV1QueryOptions.ParsePaging(page, pageSize);
            var projects = await _projectService.GetAllForUserAsync(User.GetUserId(), User.GetRole(), includeArchived);

            IEnumerable<Project> filtered = projects;
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var sorted = ApiV1QueryOptions.ApplySort(filtered, sort, SortSelectors) ?? filtered.OrderBy(p => p.Name);
            var materialized = sorted.ToList();

            var pageItems = materialized
                .Skip((clampedPage - 1) * clampedPageSize)
                .Take(clampedPageSize)
                .Select(p => p.ToV1Dto())
                .ToList();

            return Ok(new PagedResult<ProjectV1Dto>
            {
                Items = pageItems,
                Page = clampedPage,
                PageSize = clampedPageSize,
                TotalCount = materialized.Count
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProjectV1Dto>> GetById(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(project.ToV1Dto());
        }

        [HttpPost]
        public async Task<ActionResult<ProjectV1Dto>> Create([FromBody] CreateProjectV1Request request)
        {
            var project = await _projectService.CreateAsync(
                new CreateProjectRequest { Name = request.Name, Description = request.Description },
                User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id = project.Id }, project.ToV1Dto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ProjectV1Dto>> Update(Guid id, [FromBody] UpdateProjectV1Request request)
        {
            var project = await _projectService.UpdateAsync(id,
                new UpdateProjectRequest { Name = request.Name, Description = request.Description },
                User.GetUserId(), User.GetRole());
            return Ok(project.ToV1Dto());
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Archive(Guid id)
        {
            await _projectService.ArchiveAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }
    }
}
