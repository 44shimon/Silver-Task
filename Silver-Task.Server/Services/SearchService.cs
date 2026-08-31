using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Silver_Task.Server.Common;
using Silver_Task.Server.Data;
using Silver_Task.Server.Models.DTOs.Search;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Models.Entities.Enums;

namespace Silver_Task.Server.Services
{
    public interface ISearchService
    {
        Task<SearchResponseDto> SearchAsync(SearchRequest request, Guid callerId, UserRole callerRole);
    }

    /// <summary>
    /// Phase 42 — the single cross-entity search aggregator. Follows the exact same architectural
    /// pattern ReportingService already established (Phase 38): a service that holds AppDbContext
    /// directly and writes its own scoped LINQ-to-Entities queries across many entity types,
    /// rather than trying to funnel through each entity's own service methods (which weren't
    /// designed for cross-project/cross-task aggregation). Every query is parameterized LINQ —
    /// nothing here ever concatenates user input into SQL (spec #82/#83's own explicit test
    /// cases are satisfied structurally by using EF.Functions.ILike with a bound parameter, never
    /// string interpolation into raw SQL).
    ///
    /// No separate search engine/index is introduced (spec #47's own "not unless there is a
    /// genuine architectural requirement") — every query here is the same bounded,
    /// project-accessibility-scoped Postgres ILIKE pattern TaskService.SearchAsync already uses
    /// successfully; this app's per-user working set (their own projects' tasks/files/comments)
    /// is nowhere near the scale that would justify a dedicated index.
    /// </summary>
    public class SearchService(AppDbContext db, IProjectAccessService projectAccess, ITemplateService templateService) : ISearchService
    {
        private readonly AppDbContext _db = db;
        private readonly ITemplateService _templateService = templateService;
        private readonly IProjectAccessService _projectAccess = projectAccess;

        private const int MinQueryLength = 2;
        private const int CandidateLimitPerType = 100;

        private static readonly string[] AllTypes = ["task", "project", "user", "file", "comment", "tag", "template"];

        public async Task<SearchResponseDto> SearchAsync(SearchRequest request, Guid callerId, UserRole callerRole)
        {
            var isAdmin = callerRole == UserRole.Administrator;
            var accessibleProjectIds = await GetAccessibleProjectIdsAsync(callerId, isAdmin);

            var (freeText, filters) = await ParseOperatorsAsync(request, accessibleProjectIds);
            var trimmed = freeText.Trim();

            if (trimmed.Length < MinQueryLength)
            {
                return new SearchResponseDto
                {
                    Query = request.Query,
                    Total = 0,
                    Page = 1,
                    PageSize = filters.PageSize,
                    Counts = new SearchCountsDto(),
                    Results = []
                };
            }

            var isExactPhrase = trimmed.Length > 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"');
            var phrase = isExactPhrase ? trimmed.Trim('"') : trimmed;
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0)
            {
                words = [phrase];
            }

            if (filters.ProjectId is Guid requestedProjectId)
            {
                // Narrowing to a project the caller doesn't have access to must yield zero
                // results, never fall back to "search everything" — the same fail-closed rule
                // every other project-scoped query in this app already follows.
                accessibleProjectIds = isAdmin || accessibleProjectIds.Contains(requestedProjectId)
                    ? new HashSet<Guid> { requestedProjectId }
                    : [];
            }

            var wantedTypes = ParseTypes(filters.Type);
            var page = Math.Max(filters.Page, 1);
            var pageSize = Math.Clamp(filters.PageSize, 1, 100);

            // A private custom field's VALUE is already redacted from the DTO after the query
            // (CustomFieldPrivacy.Redact*Values below) — but that alone isn't enough: if the raw
            // SQL WHERE clause is still allowed to match against a private value the caller can't
            // see, a search hit on that row would itself leak "something here matches your
            // query" (spec #94's own explicit "cannot DISCOVER the field/value", not just "cannot
            // see the value"). managedProjectIds gates private-field matching at the query level,
            // not just at display time — a project the caller doesn't own/manage never matches on
            // a private field's content, full stop. A conservative approximation of
            // CustomFieldPrivacy.CanSeeValue (it ignores the rarer VisibleToRoles override, which
            // would require a per-row dictionary lookup EF can't translate to SQL) — the
            // consequence is only ever a false negative (a role-granted user might not find a row
            // via that private field's content), never a false positive/leak.
            var managedProjectIds = isAdmin
                ? accessibleProjectIds
                : (await _db.Projects
                    .Where(p => accessibleProjectIds.Contains(p.Id) &&
                        (p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId && m.Role == ProjectRole.Manager)))
                    .Select(p => p.Id)
                    .ToListAsync()).ToHashSet();

            var counts = new SearchCountsDto();
            var results = new List<SearchResultDto>();

            if (wantedTypes.Contains("task"))
            {
                var (items, count) = await SearchTasksAsync(phrase, words, accessibleProjectIds, managedProjectIds, filters, callerId, callerRole);
                counts.Tasks = count;
                results.AddRange(items);
            }
            if (wantedTypes.Contains("project"))
            {
                var (items, count) = await SearchProjectsAsync(words, accessibleProjectIds, managedProjectIds, callerId, callerRole);
                counts.Projects = count;
                results.AddRange(items);
            }
            if (wantedTypes.Contains("user") && isAdmin)
            {
                // Only an Administrator can search users at all — GET /api/users is already
                // Administrator-only (UsersController), and there is no non-admin "look up any
                // user" endpoint anywhere in this app to extend (confirmed by research before
                // writing this service). Never widened here, per spec #7's own "do not expose
                // private user information" and #75's "do not automatically give every
                // administrator access to every organization's data" (N/A here — single tenant —
                // but the *principle*, never invent a new exposure surface, still applies).
                var (items, count) = await SearchUsersAsync(phrase, words);
                counts.Users = count;
                results.AddRange(items);
            }
            if (wantedTypes.Contains("file"))
            {
                var (items, count) = await SearchFilesAsync(words, accessibleProjectIds);
                counts.Files = count;
                results.AddRange(items);
            }
            if (wantedTypes.Contains("comment"))
            {
                var (items, count) = await SearchCommentsAsync(words, accessibleProjectIds);
                counts.Comments = count;
                results.AddRange(items);
            }
            if (wantedTypes.Contains("tag"))
            {
                var (items, count) = await SearchTagsAsync(words);
                counts.Tags = count;
                results.AddRange(items);
            }
            if (wantedTypes.Contains("template"))
            {
                var (items, count) = await SearchTemplatesAsync(words, callerId, callerRole);
                counts.Templates = count;
                results.AddRange(items);
            }

            var total = counts.Tasks + counts.Projects + counts.Users + counts.Files + counts.Comments + counts.Tags + counts.Templates;

            var sorted = ApplySort(results, filters.Sort);
            var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new SearchResponseDto
            {
                Query = request.Query,
                Total = total,
                Page = page,
                PageSize = pageSize,
                Counts = counts,
                Results = paged
            };
        }

        private static List<SearchResultDto> ApplySort(List<SearchResultDto> results, string? sort) => sort switch
        {
            "newest" => results.OrderByDescending(r => r.CreatedAt).ToList(),
            "oldest" => results.OrderBy(r => r.CreatedAt).ToList(),
            "updated" => results.OrderByDescending(r => r.UpdatedAt).ToList(),
            "dueDate" => results.OrderBy(r => r.DueDate ?? DateOnly.MaxValue).ToList(),
            _ => results.OrderByDescending(r => r.Score).ThenByDescending(r => r.UpdatedAt).ToList(),
        };

        private async Task<HashSet<Guid>> GetAccessibleProjectIdsAsync(Guid callerId, bool isAdmin)
        {
            if (isAdmin)
            {
                return (await _db.Projects.Select(p => p.Id).ToListAsync()).ToHashSet();
            }

            var ids = await _db.Projects
                .Where(p => p.OwnerId == callerId || p.Members.Any(m => m.UserId == callerId))
                .Select(p => p.Id)
                .ToListAsync();
            return ids.ToHashSet();
        }

        // ---------- Text-like custom field types (Phase 41 integration) ----------

        /// <summary>Only field types whose stored Value is itself human-readable text are worth
        /// ILIKE-matching against a free-text query — Dropdown/MultiSelect/User/UserMulti/
        /// TaskReference/ProjectReference store raw ids, which would never meaningfully match a
        /// human's search term. Text/LongText were already covered by TaskService.SearchAsync;
        /// this list extends that same idea to every other genuinely-textual type (spec #11/#39's
        /// own "BP-2026-1034" example is exactly this — a Text field's value).</summary>
        private static readonly CustomFieldType[] TextLikeCustomFieldTypes =
        [
            CustomFieldType.Text, CustomFieldType.LongText, CustomFieldType.Url,
            CustomFieldType.Email, CustomFieldType.Phone, CustomFieldType.Number, CustomFieldType.Currency
        ];

        // ---------- Per-entity search ----------

        private async Task<(List<SearchResultDto> Items, int Count)> SearchTasksAsync(
            string phrase, string[] words, HashSet<Guid> accessibleProjectIds, HashSet<Guid> managedProjectIds, SearchRequest filters, Guid callerId, UserRole callerRole)
        {
            if (accessibleProjectIds.Count == 0)
            {
                return ([], 0);
            }

            var query = _db.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.CustomValues).ThenInclude(v => v.CustomField)
                .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
                .Where(t => accessibleProjectIds.Contains(t.ProjectId));

            if (filters.Status is TaskItemStatus status) query = query.Where(t => t.Status == status);
            if (filters.Priority is TaskPriority priority) query = query.Where(t => t.Priority == priority);
            if (filters.AssigneeId is Guid assigneeId) query = query.Where(t => t.AssignedToUserId == assigneeId);
            if (filters.TagId is Guid tagId) query = query.Where(t => t.TaskTags.Any(tt => tt.TagId == tagId));
            if (filters.DateFrom is DateOnly dateFrom) query = query.Where(t => t.DueDate != null && t.DueDate >= dateFrom);
            if (filters.DateTo is DateOnly dateTo) query = query.Where(t => t.DueDate != null && t.DueDate <= dateTo);

            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(t =>
                    EF.Functions.ILike(t.Title, pattern) ||
                    (t.Description != null && EF.Functions.ILike(t.Description, pattern)) ||
                    t.CustomValues.Any(v => v.Value != null && TextLikeCustomFieldTypes.Contains(v.CustomField!.FieldType)
                        && (!v.CustomField!.IsPrivate || managedProjectIds.Contains(t.ProjectId))
                        && EF.Functions.ILike(v.Value, pattern)) ||
                    t.TaskTags.Any(tt => EF.Functions.ILike(tt.Tag!.Name, pattern)));
            }

            var count = await query.CountAsync();
            var candidates = await query.OrderByDescending(t => t.UpdatedAt).Take(CandidateLimitPerType).ToListAsync();

            var projectNames = await _db.Projects.Where(p => accessibleProjectIds.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.OwnerId }).ToDictionaryAsync(p => p.Id, p => p);
            var isAdmin = callerRole == UserRole.Administrator;
            var roleByProject = new Dictionary<Guid, ProjectRole?>();

            var results = new List<SearchResultDto>();
            foreach (var task in candidates)
            {
                if (!isAdmin && !roleByProject.ContainsKey(task.ProjectId))
                {
                    roleByProject[task.ProjectId] = await _projectAccess.GetProjectRoleAsync(task.ProjectId, callerId);
                }
                var projectOwnerId = projectNames.GetValueOrDefault(task.ProjectId)?.OwnerId ?? Guid.Empty;
                CustomFieldPrivacy.RedactTaskValues(task, callerId, callerRole, projectOwnerId, isAdmin ? null : roleByProject[task.ProjectId]);

                var (score, snippet) = ScoreAndSnippet(task.Title, task.Description, task.CustomValues.Select(v => (v.CustomField?.Name ?? "", v.Value ?? "")), phrase);

                results.Add(new SearchResultDto
                {
                    Type = "Task",
                    Id = task.Id,
                    Title = task.Title,
                    Snippet = snippet,
                    ActionUrl = $"/projects/{task.ProjectId}?task={task.Id}",
                    Score = score,
                    ProjectId = task.ProjectId,
                    ProjectName = projectNames.GetValueOrDefault(task.ProjectId)?.Name,
                    Status = task.Status.ToString(),
                    Priority = task.Priority.ToString(),
                    AssigneeName = task.AssignedTo?.Name,
                    DueDate = task.DueDate,
                    TagNames = task.TaskTags.Select(tt => tt.Tag?.Name ?? "").Where(n => n.Length > 0).ToList(),
                    CreatedAt = task.CreatedAt,
                    UpdatedAt = task.UpdatedAt
                });
            }

            return (results, count);
        }

        private async Task<(List<SearchResultDto> Items, int Count)> SearchProjectsAsync(
            string[] words, HashSet<Guid> accessibleProjectIds, HashSet<Guid> managedProjectIds, Guid callerId, UserRole callerRole)
        {
            if (accessibleProjectIds.Count == 0)
            {
                return ([], 0);
            }

            var query = _db.Projects
                .Include(p => p.Owner)
                .Include(p => p.CustomValues).ThenInclude(v => v.CustomField)
                .Where(p => accessibleProjectIds.Contains(p.Id) && !p.IsArchived);

            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, pattern) ||
                    (p.Description != null && EF.Functions.ILike(p.Description, pattern)) ||
                    p.CustomValues.Any(v => v.Value != null && TextLikeCustomFieldTypes.Contains(v.CustomField!.FieldType)
                        && (!v.CustomField!.IsPrivate || managedProjectIds.Contains(p.Id))
                        && EF.Functions.ILike(v.Value, pattern)));
            }

            var count = await query.CountAsync();
            var candidates = await query.OrderByDescending(p => p.UpdatedAt).Take(CandidateLimitPerType).ToListAsync();

            var candidateIds = candidates.Select(p => p.Id).ToList();
            var taskCounts = await _db.Tasks
                .Where(t => candidateIds.Contains(t.ProjectId))
                .GroupBy(t => t.ProjectId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var isAdmin = callerRole == UserRole.Administrator;
            var phraseForScoring = string.Join(' ', words);

            // Batch-loaded once instead of one GetProjectRoleAsync call per candidate (Phase 47
            // perf audit finding) — mirrors the same fix already applied to SearchTasksAsync's
            // roleByProject dictionary a few lines above in this file.
            var roleByProject = new Dictionary<Guid, ProjectRole?>();
            if (!isAdmin)
            {
                roleByProject = await _db.ProjectMembers
                    .Where(m => candidateIds.Contains(m.ProjectId) && m.UserId == callerId)
                    .ToDictionaryAsync(m => m.ProjectId, m => (ProjectRole?)m.Role);
            }

            var results = new List<SearchResultDto>();
            foreach (var project in candidates)
            {
                var callerProjectRole = isAdmin ? null : roleByProject.GetValueOrDefault(project.Id);
                CustomFieldPrivacy.RedactProjectValues(project, callerId, callerRole, callerProjectRole);

                var (score, snippet) = ScoreAndSnippet(project.Name, project.Description, project.CustomValues.Select(v => (v.CustomField?.Name ?? "", v.Value ?? "")), phraseForScoring);

                results.Add(new SearchResultDto
                {
                    Type = "Project",
                    Id = project.Id,
                    Title = project.Name,
                    Snippet = snippet ?? $"Project Manager: {project.Owner?.Name}",
                    ActionUrl = $"/projects/{project.Id}",
                    Score = score,
                    Status = project.IsArchived ? "Archived" : "Active",
                    AssigneeName = project.Owner?.Name,
                    TagNames = [$"{taskCounts.GetValueOrDefault(project.Id)} tasks"],
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt
                });
            }

            return (results, count);
        }

        /// <summary>Administrator-only (see the caller's own gate) — mirrors GET /api/users'
        /// existing Administrator-only authorization exactly rather than inventing a wider
        /// exposure. Searches Name and Email (spec #38), since only an Administrator ever reaches
        /// this branch.</summary>
        private async Task<(List<SearchResultDto> Items, int Count)> SearchUsersAsync(string phrase, string[] words)
        {
            var query = _db.Users.AsQueryable();
            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern));
            }

            var count = await query.CountAsync();
            var candidates = await query.OrderBy(u => u.Name).Take(CandidateLimitPerType).ToListAsync();

            var results = candidates.Select(u => new SearchResultDto
            {
                Type = "User",
                Id = u.Id,
                Title = u.Name,
                Snippet = u.Email,
                ActionUrl = $"/admin/users?userId={u.Id}",
                Score = NameScore(u.Name, phrase),
                Status = u.IsActive ? "Active" : "Inactive",
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.CreatedAt
            }).ToList();

            return (results, count);
        }

        /// <summary>A new cross-project file search — AttachmentService.GetAllForProjectAsync's
        /// own `search` filter (filename+description ILIKE) already exists but is scoped to one
        /// project at a time; this reuses the exact same two-column ILIKE predicate, just applied
        /// across every project the caller can access, mirroring how TaskService.SearchAsync is
        /// itself a cross-project generalization of what a single project's task list already
        /// filters on.</summary>
        private async Task<(List<SearchResultDto> Items, int Count)> SearchFilesAsync(string[] words, HashSet<Guid> accessibleProjectIds)
        {
            if (accessibleProjectIds.Count == 0)
            {
                return ([], 0);
            }

            var query = _db.Attachments
                .Include(a => a.UploadedBy)
                .Include(a => a.FileTags).ThenInclude(ft => ft.Tag)
                .Include(a => a.Project)
                .Include(a => a.Task).ThenInclude(t => t!.Project)
                .Include(a => a.Comment).ThenInclude(c => c!.Task).ThenInclude(t => t!.Project)
                .Where(a => !a.IsDeleted);

            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(a =>
                    EF.Functions.ILike(a.FileName, pattern) ||
                    (a.Description != null && EF.Functions.ILike(a.Description, pattern)) ||
                    a.FileTags.Any(ft => EF.Functions.ILike(ft.Tag!.Name, pattern)));
            }

            var candidatesRaw = await query.OrderByDescending(a => a.UpdatedAt).Take(CandidateLimitPerType * 2).ToListAsync();

            // Resolve each file's effective project (Project/Task/Comment polymorphic parent —
            // same AttachmentMappingExtensions.ResolveEffectiveProjectId concept) and drop any
            // the caller can no longer access, same as AttachmentService.FilterToCurrentlyAccessibleAsync.
            var accessible = candidatesRaw
                .Select(a => (Attachment: a, ProjectId: a.ProjectId ?? a.Task?.ProjectId ?? a.Comment?.Task?.ProjectId))
                .Where(x => x.ProjectId is Guid pid && accessibleProjectIds.Contains(pid))
                .Take(CandidateLimitPerType)
                .ToList();

            var count = accessible.Count;
            var phrase = string.Join(' ', words);

            var results = accessible.Select(x =>
            {
                var a = x.Attachment;
                var projectId = x.ProjectId!.Value;
                var projectName = a.Project?.Name ?? a.Task?.Project?.Name ?? a.Comment?.Task?.Project?.Name;
                var (score, _) = ScoreAndSnippet(a.FileName, a.Description, [], phrase);
                return new SearchResultDto
                {
                    Type = "File",
                    Id = a.Id,
                    Title = a.FileName,
                    Snippet = a.Description,
                    ActionUrl = a.TaskId is Guid taskId ? $"/projects/{projectId}?task={taskId}" : $"/projects/{projectId}?view=files",
                    Score = score,
                    ProjectId = projectId,
                    ProjectName = projectName,
                    TagNames = a.FileTags.Select(ft => ft.Tag?.Name ?? "").Where(n => n.Length > 0).ToList(),
                    FileSizeBytes = a.FileSize,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                };
            }).ToList();

            return (results, count);
        }

        /// <summary>A new cross-task comment CONTENT search — no equivalent existed anywhere
        /// (CommentService only ever lists one task's comments). Scoped by the same accessible-
        /// project predicate as everything else; comment edit/delete stays author-only elsewhere
        /// in the app, but *viewing* a comment already follows the participate tier (any project
        /// member can read any comment on a task they can see), so surfacing it in search needs
        /// no additional per-comment check beyond "is this comment's task in an accessible
        /// project."</summary>
        private async Task<(List<SearchResultDto> Items, int Count)> SearchCommentsAsync(string[] words, HashSet<Guid> accessibleProjectIds)
        {
            if (accessibleProjectIds.Count == 0)
            {
                return ([], 0);
            }

            var query = _db.TaskComments
                .Include(c => c.User)
                .Include(c => c.Task).ThenInclude(t => t!.Project)
                .Where(c => accessibleProjectIds.Contains(c.Task!.ProjectId));

            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(c => EF.Functions.ILike(c.Text, pattern));
            }

            var count = await query.CountAsync();
            var candidates = await query.OrderByDescending(c => c.UpdatedAt).Take(CandidateLimitPerType).ToListAsync();
            var phrase = string.Join(' ', words);

            var results = candidates.Select(c => new SearchResultDto
            {
                Type = "Comment",
                Id = c.Id,
                Title = c.Task!.Title,
                Snippet = Truncate(c.Text, 160),
                ActionUrl = $"/projects/{c.Task.ProjectId}?task={c.TaskId}",
                Score = NameScore(c.Text, phrase) > 0 ? CommentScore + 10 : CommentScore,
                ProjectId = c.Task.ProjectId,
                ProjectName = c.Task.Project?.Name,
                AssigneeName = c.User?.Name,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToList();

            return (results, count);
        }

        private async Task<(List<SearchResultDto> Items, int Count)> SearchTagsAsync(string[] words)
        {
            var query = _db.Tags.Where(t => t.IsActive);
            foreach (var word in words)
            {
                var pattern = $"%{word}%";
                query = query.Where(t => EF.Functions.ILike(t.Name, pattern) || (t.Description != null && EF.Functions.ILike(t.Description, pattern)));
            }

            var count = await query.CountAsync();
            var candidates = await query.OrderBy(t => t.Name).Take(CandidateLimitPerType).ToListAsync();
            var phrase = string.Join(' ', words);

            var results = candidates.Select(t => new SearchResultDto
            {
                Type = "Tag",
                Id = t.Id,
                Title = t.Name,
                Snippet = t.Description,
                ActionUrl = $"/search?q={Uri.EscapeDataString(t.Name)}&tagId={t.Id}",
                Score = NameScore(t.Name, phrase),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.CreatedAt
            }).ToList();

            return (results, count);
        }

        /// <summary>Reuses ITemplateService.ListForCallerAsync's own already-authorized list
        /// (owned/shared/Public per Phase 40's own visibility rules) rather than a second
        /// permission-aware template query — this just filters that already-safe list in memory,
        /// which is fine at this app's template-count scale (the same reasoning
        /// TemplateUsageReport's own "top 10" in-memory sort already relies on).</summary>
        private async Task<(List<SearchResultDto> Items, int Count)> SearchTemplatesAsync(string[] words, Guid callerId, UserRole callerRole)
        {
            var all = await _templateService.ListForCallerAsync(callerId, callerRole);
            var phrase = string.Join(' ', words);

            var matches = all.Where(t => !t.IsArchived && words.All(w =>
                t.Name.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                (t.Description?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false))).ToList();

            var results = matches.Select(t => new SearchResultDto
            {
                Type = "Template",
                Id = t.Id,
                Title = t.Name,
                Snippet = t.Description,
                ActionUrl = t.Type == "Project" ? $"/templates/project/{t.Id}" : $"/templates/task/{t.Id}",
                Score = NameScore(t.Name, phrase),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return (results, matches.Count);
        }

        // ---------- Scoring helpers ----------

        private static double NameScore(string name, string phrase)
        {
            var lowerName = name.ToLowerInvariant();
            var lowerPhrase = phrase.ToLowerInvariant();
            if (lowerName == lowerPhrase) return NameExactScore;
            if (lowerName.StartsWith(lowerPhrase, StringComparison.Ordinal)) return 80;
            if (lowerName.Contains(lowerPhrase, StringComparison.Ordinal)) return NameContainsScore;
            return 45; // Matched via a multi-word AND across other fields, not the name itself.
        }

        private static (double Score, string? Snippet) ScoreAndSnippet(string title, string? description, IEnumerable<(string FieldName, string Value)> customValues, string phrase)
        {
            var lowerPhrase = phrase.ToLowerInvariant();
            if (title.Contains(phrase, StringComparison.OrdinalIgnoreCase) || title.ToLowerInvariant().Split(' ').Any(w => lowerPhrase.Contains(w)))
            {
                return (TitleScore(title, phrase, phrase), null);
            }
            if (description is not null && description.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return (DescriptionScore, Truncate(description, 160));
            }
            foreach (var (fieldName, value) in customValues)
            {
                if (value.Length > 0 && value.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    return (CustomFieldScore, $"{fieldName}: {value}");
                }
            }
            // Matched via the multi-word AND clause (each word present somewhere) without any
            // single field containing the whole phrase contiguously.
            return (50, null);
        }

        private static string Truncate(string text, int maxLength) =>
            text.Length <= maxLength ? text : text[..maxLength] + "…";

        // ---------- Ranking ----------

        /// <summary>Spec #15's own priority order, translated to numeric weights: exact title >
        /// title starts-with > title contains > description > custom-field > comment. Identifier
        /// match is skipped — this app has no separate short identifier for tasks/projects (GUID
        /// only), confirmed by research; see the Phase 42 final report's own disclosed note.</summary>
        private static double TitleScore(string title, string query, string phrase)
        {
            var lowerTitle = title.ToLowerInvariant();
            var lowerQuery = query.ToLowerInvariant();
            if (lowerTitle == lowerQuery) return 100;
            if (lowerTitle.StartsWith(lowerQuery, StringComparison.Ordinal)) return 80;
            if (lowerTitle.Contains(phrase, StringComparison.OrdinalIgnoreCase)) return 65;
            return 60; // Matched via multi-word AND but not as a contiguous substring of the title.
        }

        private const double DescriptionScore = 40;
        private const double CustomFieldScore = 30;
        private const double CommentScore = 20;
        private const double NameExactScore = 100;
        private const double NameContainsScore = 55;

        // ---------- Query parsing ----------

        private static readonly Regex OperatorToken = new(
            "(?<key>status|priority|assignee|project|tag):(?:\"(?<qval>[^\"]*)\"|(?<val>\\S+))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Extracts status:/priority:/assignee:/project:/tag: tokens (spec #31) out of
        /// the raw query text, resolving name-based operators (assignee/project/tag) against the
        /// SAME accessible-project scope the rest of the search already uses — never a separate,
        /// unscoped lookup. Everything left over after removing operator tokens is the free-text
        /// query. Pure string parsing + LINQ lookups, never raw SQL (spec #32).</summary>
        private async Task<(string FreeText, SearchRequest Filters)> ParseOperatorsAsync(SearchRequest request, HashSet<Guid> accessibleProjectIds)
        {
            var remaining = request.Query;
            var matches = OperatorToken.Matches(request.Query);

            var result = new SearchRequest
            {
                Query = request.Query,
                Type = request.Type,
                ProjectId = request.ProjectId,
                Status = request.Status,
                Priority = request.Priority,
                AssigneeId = request.AssigneeId,
                TagId = request.TagId,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                Page = request.Page,
                PageSize = request.PageSize,
                Sort = request.Sort
            };

            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value.ToLowerInvariant();
                var value = match.Groups["qval"].Success ? match.Groups["qval"].Value : match.Groups["val"].Value;
                remaining = remaining.Replace(match.Value, " ");

                switch (key)
                {
                    case "status":
                        if (Enum.TryParse<TaskItemStatus>(value, true, out var status)) result.Status = status;
                        break;
                    case "priority":
                        if (Enum.TryParse<TaskPriority>(value, true, out var priority)) result.Priority = priority;
                        break;
                    case "assignee":
                        result.AssigneeId = await ResolveAssigneeIdByNameAsync(value, accessibleProjectIds);
                        break;
                    case "project":
                        result.ProjectId = await ResolveProjectIdByNameAsync(value, accessibleProjectIds);
                        break;
                    case "tag":
                        // Tags are a global, shared vocabulary any authenticated user can already
                        // see in full (ITagService.GetActiveAsync — no project scoping), so no
                        // extra access check is needed here.
                        result.TagId = await ResolveTagIdByNameAsync(value);
                        break;
                }
            }

            return (remaining.Trim(), result);
        }

        /// <summary>Scoped to members of the caller's own accessible projects — never a global
        /// user lookup (there is no non-admin "search all users" endpoint anywhere in this app;
        /// see the Phase 42 final report's own disclosed note), so `assignee:someone` can never
        /// reveal that a user named "someone" exists unless they already share a project with
        /// the caller.</summary>
        private Task<Guid?> ResolveAssigneeIdByNameAsync(string namePart, HashSet<Guid> accessibleProjectIds) =>
            _db.ProjectMembers
                .Where(m => accessibleProjectIds.Contains(m.ProjectId) && EF.Functions.ILike(m.User!.Name, $"%{namePart}%"))
                .Select(m => (Guid?)m.UserId)
                .FirstOrDefaultAsync();

        private Task<Guid?> ResolveProjectIdByNameAsync(string namePart, HashSet<Guid> accessibleProjectIds) =>
            _db.Projects
                .Where(p => accessibleProjectIds.Contains(p.Id) && EF.Functions.ILike(p.Name, $"%{namePart}%"))
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

        private Task<Guid?> ResolveTagIdByNameAsync(string namePart) =>
            _db.Tags.Where(t => t.IsActive && EF.Functions.ILike(t.Name, $"%{namePart}%")).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();

        private static HashSet<string> ParseTypes(string? type)
        {
            if (string.IsNullOrWhiteSpace(type) || type.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return AllTypes.ToHashSet();
            }
            var requested = type.ToLowerInvariant();
            return AllTypes.Contains(requested) ? [requested] : [];
        }
    }
}
