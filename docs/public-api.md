# Public API (Phase 61)

This document covers the versioned public API foundation: what it is, the conventions it
establishes, what it deliberately does and doesn't cover yet, and how to extend it. For the
existing internal API the SPA itself uses (`api/<resource>`, unversioned), nothing here changes —
see `CLAUDE.md`/`README.md`'s own architecture sections for that.

## Why this exists

Every internal endpoint (`Controllers/*.cs`) is unversioned and consumed directly by the SPA —
fine for a single first-party client that deploys in lockstep with the server, but not something
an external integration should build against: any internal DTO can change shape the moment the SPA
needs it to. `/api/v1/*` (`Controllers/V1/*.cs`) is a **separate, deliberately stable surface**
with its own request/response contract, versioned so a future v2 can be added without breaking v1
callers, and a future v1 breaking change would instead become v2.

## Scope of this phase — a reference implementation, not full parity

This phase implements **two resources** end-to-end — Projects and Tasks
(`Controllers/V1/ProjectsController.cs`, `Controllers/V1/TasksController.cs`) — chosen because
between them they exercise every convention below (pagination, filtering, sorting, search, nested
resource references, full CRUD, validation). The other ~27 internal resources (Comments,
Attachments, Custom Fields, Automations, Admin\*, ...) are **not** yet available under `/api/v1/`.
Porting them is real, valuable follow-up work for a future phase — this phase's job was to prove
the pattern and make it trivial to repeat, not to port everything at once.

## Versioning scheme

URL-path versioning: `/api/v{n}/...`. A new major version means a **new folder**
(`Controllers/V2/`), a **new route prefix** (`api/v2/`), and its own DTOs — the existing `V1`
folder keeps running completely unchanged. No `Asp.Versioning.*` package is used; a route-prefix +
folder convention is sufficient for a single live version and is exactly the pattern a real v2
would repeat. Revisit this decision only if a future need (content negotiation, header-based
versioning) actually requires it.

## Resource & URI conventions

- Plural nouns, lowercase: `api/v1/projects`, `api/v1/tasks`.
- Collection endpoints are **flat**, not deeply nested — `GET /api/v1/tasks?projectId=...`, not
  `GET /api/v1/projects/{id}/tasks` (the internal API's shape). This lets a client address a
  single task directly (`GET /api/v1/tasks/{id}`) without first knowing its project — the more
  RESTful shape for a resource external integrations will often already have an id for.
- Standard verbs: `GET` (list/detail), `POST` (create), `PUT` (full-resource replace — matches the
  internal API's own PUT semantics, see `UpdateTaskRequest`'s doc comment), `DELETE`.
- `DELETE` on a project archives it (`IProjectService.ArchiveAsync`) — matches the internal API's
  own archive-not-hard-delete semantics; there is no hard-delete for projects anywhere in this
  app.
- Status codes: `200` (success), `201` (created, with a `Location` header via `CreatedAtAction`),
  `204` (deleted/no content), `400` (validation), `401` (not authenticated), `403` (not
  authorized), `404` (not found), `409` (conflict) — the same domain-exception-to-status mapping
  `ExceptionHandlingMiddleware` already provides for every controller, internal or v1.

## Authentication & authorization integration points

**Today**: `/api/v1/*` requires the exact same authenticated session as every internal endpoint —
the global `FallbackPolicy` in `Program.cs` (`RequireAuthenticatedUser()`) applies to it exactly
like anything else without an explicit `[AllowAnonymous]`. There is no separate v1 auth mechanism.
Authorization is enforced entirely inside the reused services (`IProjectService`/`ITaskService`
call into `ProjectAccessService`'s existing tiers) — the v1 controllers add zero new authorization
logic.

**Why not API keys yet**: this phase's own instructions say not to build them unless the existing
architecture requires temporary internal auth for testing — it doesn't; the existing cookie
session was sufficient to build and verify this foundation. More fundamentally, there is currently
no way for a non-browser client to *obtain* credentials at all: `POST /api/auth/login` sets an
httpOnly cookie and never returns the raw JWT in the response body (a deliberate decision — see
`README.md`/`CLAUDE.md`). Building API keys before this foundation existed would have meant
inventing throwaway auth just to test it, which is its own risk.

**The integration point for a future phase**: ASP.NET Core supports multiple named authentication
schemes simultaneously. A future API-key (or OAuth client-credentials) scheme would:

1. Register a second scheme in `Program.cs` alongside the existing
   `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` — e.g.
   `.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", ...)`.
2. Add a named authorization policy (`options.AddPolicy("ApiKeyOrCookie", p =>
   p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "ApiKey").RequireAuthenticatedUser())`)
   applied only to `Controllers/V1/*` (via `[Authorize(Policy = "ApiKeyOrCookie")]` on those
   controllers specifically) — the cookie scheme and its `FallbackPolicy` stay exactly as they are
   for every internal controller.
3. API keys themselves would need their own storage (a new table, hashed at rest — never plain —
   mirroring how `User.PasswordHash` is already handled), issuance/revocation UI (likely an
   `Admin*` controller, following the existing Admin\* pattern), and rate limiting per key (the
   existing `AddRateLimiter`/`[EnableRateLimiting]` mechanism from Phase 59 already generalizes to
   this — a new named policy, not new infrastructure).

None of the above is built in this phase. It's documented here so a future phase has a concrete,
low-risk plug-in point rather than needing to rediscover it.

## Request validation

Standard ASP.NET Core `[Required]`/`[StringLength]` data annotations on the v1 request DTOs
(`Models/DTOs/V1/*.cs`) — identical mechanism the internal API already uses. As of this phase,
**every** controller (internal and v1 alike) returns the same error shape for a validation
failure — see below.

## Response & error standards

**One error shape everywhere**: `ApiErrorResponse` (`Models/Common/ApiErrorResponse.cs`) —

```json
{ "message": "One or more validation errors occurred.", "traceId": "0HN...:00000001", "errors": { "name": ["The Name field is required."] } }
```

`errors` is `null` when there's nothing field-specific to report (e.g. a plain 404). Previously,
only exceptions caught by `ExceptionHandlingMiddleware` (404/409/403/400 domain exceptions) used
this shape — `[ApiController]`'s *automatic* 400 on a malformed request body (invalid `ModelState`,
before a controller action even runs) used to fall back to the ASP.NET Core default
`ValidationProblemDetails` (`{type, title, status, errors}`) instead — two different shapes
depending on which layer rejected the request. Phase 61 fixed this globally
(`Program.cs`'s `ConfigureApiBehaviorOptions`) — every 400 from every controller, internal or v1,
now returns `ApiErrorResponse`. Confirmed non-breaking: the SPA's `httpClient.ts` only ever reads
`.message`/`.errors` from an error body, both of which `ApiErrorResponse` already provided.

**Success responses** are the resource itself for single-item endpoints (`ProjectV1Dto`,
`TaskV1Dto`), or a `PagedResult<T>` envelope for collections (see Pagination below). No wrapper
envelope around single-item responses — REST convention, matches every internal endpoint already.

## Pagination

Every v1 list endpoint returns `PagedResult<T>` (`Models/Common/PagedResult.cs`):

```json
{ "items": [...], "page": 1, "pageSize": 25, "totalCount": 142, "totalPages": 6 }
```

Query params: `page` (default 1), `pageSize` (default 25, clamped to a max of 100). Both are
clamped, never rejected — `page=0` or `pageSize=99999` silently becomes a sane value rather than a
400, since that's almost always what the caller actually wanted. See
`Common/ApiV1QueryOptions.ParsePaging`.

This replaces three different ad-hoc shapes the internal API had already accumulated
(`EmailDeliveryPageDto`, `ProjectsController.GetFiles`'s inline anonymous-typed response,
`SearchController`'s own `page`/`pageSize`) — those internal endpoints are untouched (changing a
working internal response shape is exactly what this phase's instructions say not to do); v1
starts clean with the one canonical shape.

**Why paging lives in the v1 controller, not the service layer**: `IProjectService`/`ITaskService`
still return their full, already-authorized in-memory list (`GetAllForUserAsync`/
`GetAllForProjectAsync`) — unchanged, same as every internal caller. Phase 60 confirmed this is
fast even at 5,000 tasks and deliberately kept it that way for the SPA (client-side filtering).
That's a fine architecture for a browser client but not for a non-browser API client, which has no
"filter in JS" option — so paging/filtering/sorting/search are applied **once, at the v1 boundary**
(plain LINQ over the list the service already returned), leaving the internal services and every
internal endpoint completely untouched.

## Filtering

Resource-specific query parameters, applied after the paging/sort/search logic above:

- `GET /api/v1/projects`: `includeArchived` (bool, default false).
- `GET /api/v1/tasks`: `projectId` (**required** — a flat collection needs some scope; omitting it
  returns `400` with `{"message": "projectId is required."}`), `status`, `priority`,
  `assignedToUserId`.

## Sorting

One query parameter, `sort` — a bare field name for ascending, a `-` prefix for descending (e.g.
`sort=-createdAt`). One canonical convention (`Common/ApiV1QueryOptions.ApplySort`), replacing the
internal API's several different shapes (`SearchController`'s single sort string,
`ProjectsController.GetFiles`'s separate `sortField`/`sortDescending` pair). An unrecognized or
omitted `sort` value is never a 400 — sorting is a refinement, not a required, validatable input;
it silently falls back to each endpoint's own default order (`name` for projects, `SortOrder` for
tasks — the same manual-ordering field the Sheet view's own drag-reorder uses).

Recognized fields today: projects — `name`, `createdAt`, `updatedAt`. Tasks — `title`, `dueDate`,
`priority`, `status`, `createdAt`, `updatedAt`.

## Search

`q` — a case-insensitive substring match against the resource's name/title and description.
Same query-parameter name the internal `SearchController`/`TasksController.Search` already use, so
the convention isn't yet another new one to learn.

## API version metadata

`GET /api/v1/meta` (anonymous) —

```json
{ "apiVersion": "v1", "appVersion": "1.0.1", "supportedVersions": ["v1"] }
```

`appVersion` is the same `AssemblyInformationalVersion` (sourced from the repo-root `VERSION` file)
`GET /api/health` already exposes.

## API health

`GET /api/v1/health` (anonymous) — the same database-reachability check `GET /api/health/ready`
already performs, exposed under the v1 prefix too. **Additive only**: `/api/health` and
`/api/health/ready` (which `scripts/update-debian.sh` and any external uptime monitor already
depend on) are completely untouched.

## API documentation

- **This file** — the authoritative, always-reachable (no auth, no dev-mode) reference.
- **Machine-readable OpenAPI**: a second, named OpenAPI document (`AddOpenApi("v1", ...)` in
  `Program.cs`) scoped via `ShouldInclude` to *only* `api/v1/*` routes — never the 29 internal/
  admin controllers the existing (renamed) `"internal"` document still covers, which stays
  Development-only exactly as before.

  **Known limitation, confirmed by direct live testing, not glossed over**: `MapOpenApi("v1")` was
  intended to be reachable anonymously in every environment (`.AllowAnonymous()`), matching
  `GET /api/v1/meta`/`health`. In practice, chaining `.AllowAnonymous()` (or an equivalent
  always-succeeding `RequireAuthorization` policy — both were tried) onto `MapOpenApi(...)`'s
  returned builder does not take effect against this app's global `FallbackPolicy`, while the
  identical technique demonstrably works for every other endpoint in `Program.cs`
  (`MapStaticAssets`, `MapFallbackToFile`, a plain `MapGet`, and every `[AllowAnonymous]`-attributed
  controller including `Controllers/V1/ApiInfoController` right next to it). This appears to be a
  metadata-propagation gap specific to `MapOpenApi`'s endpoint registration in
  `Microsoft.AspNetCore.OpenApi 10.0.11`, not something fixable from application code short of
  bypassing `MapOpenApi`'s own routing (e.g. serving a pre-generated static JSON file instead) —
  judged out of scope for a foundation phase to build a workaround for. Today, `/openapi/v1.json`
  requires the same authenticated session as everything else; this markdown document is the
  reliable, anonymous reference in the meantime. Worth revisiting in a future phase.

## How to add a new v1 resource

Follow the Projects/Tasks pattern exactly:

1. New DTOs in `Models/DTOs/V1/<Resource>V1Dto.cs` (response) + `Create<Resource>V1Request`/
   `Update<Resource>V1Request` — **do not** reuse the internal DTOs; that's the whole point of the
   decoupling (see "Why this exists" above).
2. A `<Resource>V1MappingExtensions.cs` with a `ToV1Dto()` extension mapping straight from the
   entity — same pattern as `ProjectV1MappingExtensions`/`TaskV1MappingExtensions`.
3. A new `Controllers/V1/<Resource>Controller.cs` that calls the **existing** internal service
   (`I<Resource>Service`) for every operation — no new business logic, no new authorization. Use
   `ApiV1QueryOptions.ParsePaging`/`ApplySort` and return `PagedResult<T>` for any list endpoint.
4. Add the resource to this document.

## What this phase deliberately did not do

Consistent with the spec's own "do not redesign the application" / "do not replace working
internal APIs" instructions: no port of the other ~27 resources (a real follow-up, not done here),
no API keys or any other new authentication mechanism (documented as an integration point only —
see above), no rate limiting scoped specifically to external API callers (the existing IP-based
login limiter from Phase 59 is unrelated and untouched), no deprecation/changelog policy beyond
"a new version gets a new folder" (nothing to deprecate yet with only one version), no GraphQL or
any other non-REST API style, and no change whatsoever to the internal API's own routes, DTOs, or
behavior — every internal endpoint the SPA uses today behaves identically to before this phase.
