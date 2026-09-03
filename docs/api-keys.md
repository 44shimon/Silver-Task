# API keys & service accounts (Phase 62)

This document covers integration authentication: service accounts, API keys, how they're stored
and validated, expiration/rotation/revocation, the admin endpoints and UI, audit events, and the
diagnostics extension. For the versioned public API itself (resource conventions, pagination,
error shapes), see [docs/public-api.md](public-api.md) — that document's own "Authentication &
authorization integration points" section named this exact gap as future work; this phase builds
it.

## Why a service account is a `User` row

Authorization in this app (`ProjectAccessService`'s tiers, `[Authorize(Roles=...)]`, every service
method's `(callerId, callerRole)` parameters) is entirely built around the `Users` table. Rather
than inventing a second, parallel identity/permission model for machine callers, a service account
is an ordinary `User` row with `IsServiceAccount = true`:

- It has a normal `UserRole` (Administrator/Manager/Member/Viewer) and can be added to specific
  projects via the **existing, unmodified** `POST /api/projects/{id}/members` (by its
  auto-generated email) — exactly like a human member. Every authorization check anywhere in the
  app keeps working completely unmodified.
- It can never log in with a password. `PasswordHash` is still set (a random, never-disclosed,
  never-usable value — never intended to be guessable or given to anyone) purely because the
  column is non-nullable; `AuthService.LoginAsync` unconditionally rejects `IsServiceAccount`
  accounts as an explicit, separate check (defense in depth, not just "no UI creates this path").
- Its email is auto-generated: `{slugified-name}-{8-hex-chars}@service.invalid` — the same
  RFC 2606 reserved, non-routable `.invalid` TLD `PerformanceDataSeeder` (Phase 60) already uses
  for synthetic identities, so it can never be mistaken for a real address.

## API keys

An `ApiKey` row authenticates as its owning `User` — a service account, typically, though nothing
stops a key from being issued for a human user's own id. Fields: `Name` (admin-supplied label),
`KeyPrefix`, `KeyHash`, `ExpiresAt` (nullable — never-expiring is allowed, not forced),
`RevokedAt`/`RevokedByUserId`, `LastUsedAt`, `CreatedAt`/`CreatedByUserId`. **Status is computed,
never stored** — Active/Revoked/Expired, derived from `RevokedAt`/`ExpiresAt` at read time, so
there's no way for a stored status to drift out of sync with an expiration date that simply
passed.

### Format, hashing, and the display-once rule

A key looks like `stak_l6RwsH3Hetk8d0nK-trweKD4yrpyAYrR9xHnOMjGUhE` — `stak_` (Silver Task API
Key) followed by 43 URL-safe base64 characters (32 random bytes, ~256 bits of entropy, via
`RandomNumberGenerator.GetBytes`).

**Only `KeyHash` (SHA-256 hex of the full key) and `KeyPrefix` (the first 12 characters) are ever
persisted.** The full plaintext key is generated, hashed, and returned **exactly once** — in the
response body of the create and rotate endpoints only. No `GET` endpoint, no log line, and no
database column ever holds it again. This is the single most important invariant of the whole
feature.

SHA-256 (fast, deterministic) is the *correct* choice here, not a shortcut — unlike a human
password (low entropy, needs a deliberately slow hash like PBKDF2/bcrypt to resist offline
guessing), a 256-bit random key is already unguessable, so a direct indexed hash lookup
(`WHERE KeyHash = @hash`) is both sufficient and necessary: a slow hash would make *every* API
request pay a deliberate CPU cost for no corresponding security benefit against a secret this
random.

### Expiration, rotation, revocation, status

- **Expiration** is optional (`expiresAt: null` = never expires) — a sane default the Admin UI
  nudges toward (30/90/365 days or never), not a hardcoded requirement.
- **Rotation** (`POST /api/admin/api-keys/{id}/rotate`) revokes the existing key and issues a
  brand-new one with the same owner/name/expiration policy — not an in-place secret swap. Simpler,
  and matches how Stripe/GitHub actually implement rotation.
- **Revocation** (`DELETE /api/admin/api-keys/{id}`) sets `RevokedAt`/`RevokedByUserId` —
  keys are never hard-deleted, matching this app's existing soft-delete convention for `Users`.
- Deactivating a service account (`DELETE /api/admin/service-accounts/{id}`) also revokes every
  key it holds, in the same transaction.

## The `X-Api-Key` header & auth scheme

A second, named ASP.NET Core authentication scheme ("ApiKey",
`Services/ApiKeyAuthenticationHandler.cs`) coexists with — never replaces — the existing
cookie/JWT scheme the SPA uses. A new `"ApiKeyOrCookie"` authorization policy accepts either,
applied via `[Authorize(Policy = "ApiKeyOrCookie")]` only on `Controllers/V1/ProjectsController`
and `Controllers/V1/TasksController`. Every internal controller keeps relying on the global
`FallbackPolicy` (cookie only) exactly as before — this is additive only to the public v1 surface.

```bash
curl -H "X-Api-Key: stak_l6RwsH3Hetk8d0nK-trweKD4yrpyAYrR9xHnOMjGUhE" \
  https://your-instance/api/v1/tasks?projectId=<id>
```

On success, the handler builds a `ClaimsPrincipal` with exactly the claim shape
`JwtTokenService.GenerateToken` already issues for a cookie session (`ClaimTypes.NameIdentifier`/
`Name`/`Role`) — so every downstream authorization check works identically regardless of which
scheme authenticated the request. One extra `"auth_method"="apikey"` claim lets logging/
diagnostics distinguish the two without changing any authorization decision.

A missing header falls through (`NoResult()`) so the cookie scheme can still succeed on the same
request; a present-but-invalid key always fails with the same generic message regardless of
*why* (unknown/revoked/expired/inactive owner) — never revealing which check failed, mirroring
`AuthService.LoginAsync`'s own "don't tell the caller which check failed" precedent.

`LastUsedAt` is updated on success, **throttled to once per minute per key** — not a
write-per-request, which would turn a chatty integration into an unnecessary database hot path.

## Why there's no login-style lockout — and what there is instead

Phase 59 added IP-partitioned rate limiting for the login endpoint specifically because a human
password is comparatively low-entropy and guessable. An API key is a different threat model
entirely: ~256 bits of randomness isn't practically brute-forceable, so a login-style lockout
would be the wrong tool, and a blanket per-IP rate limit on `/api/v1/*` (the ASP.NET
`AddRateLimiter` middleware Phase 59 already uses for login) would throttle a whole endpoint's
traffic — *successes included* — which could hurt a legitimate high-volume integration (an n8n
workflow firing many valid requests).

What guards against a leaked partial key, a scripted scan, or plain misconfiguration instead:
`IApiKeyFailureTracker` (`Services/ApiKeyFailureTracker.cs`) — a lightweight, in-memory,
per-source-IP counter of **failed** attempts only (same singleton shape as
`IWorkerHeartbeatRegistry`/`ISlowOperationTracker` from Phases 58/60). Once an IP crosses the
configured threshold within the window, further attempts from it get an immediate, cheap 401
(no hashing, no database query) without adding to the count further. Configurable via
`Security:ApiKeyFailureLimit:MaxFailures` (default 10) / `Security:ApiKeyFailureLimit:WindowSeconds`
(default 300) — same config-namespace convention as `Security:LoginRateLimit:*`. This is
defense-in-depth, not the primary control (that's the key's own entropy) — it's a single-process,
in-memory tracker, not a distributed one.

## Admin endpoints (`api/admin`, Administrator-only)

All key/service-account management is Administrator-only this phase — "administer
organization-wide API credentials" is the spec's own framing. A self-service "create my own
personal key" endpoint is a reasonable future addition, not built here.

| Method & path | Purpose |
|---|---|
| `GET /admin/service-accounts` | List all service accounts. |
| `POST /admin/service-accounts` | Create one (`{name, role}`). |
| `DELETE /admin/service-accounts/{id}` | Deactivate (also revokes its keys). |
| `GET /admin/api-keys` | List all keys, any owner. |
| `GET /admin/api-keys/{id}` | Single key detail (never the raw key). |
| `POST /admin/api-keys` | Create (`{userId, name, expiresAt?}`) — `201` with the plaintext key **once**. |
| `POST /admin/api-keys/{id}/rotate` | Revoke + reissue — new plaintext key **once**. |
| `DELETE /admin/api-keys/{id}` | Revoke. |

## Admin UI

Admin → API Keys (`silver-task.client/src/pages/admin/AdminApiKeysPage.tsx`) — two tables (API
Keys, Service Accounts) and a "New API Key" flow that can create a new service account inline or
target an existing one. After create/rotate, a dedicated dialog shows the raw key exactly once
with a copy-to-clipboard button and an explicit "this will never be shown again" warning — closing
it is the only way to dismiss it, and there's no way to reopen it afterward (the page never
requests or caches the raw value anywhere beyond that one dialog's own component state).

## Audit events

Every mutating operation logs via the same `_logger.LogWarning("Admin action: ...", ...)`
convention Phase 59 established in `UserService` — service account created/deactivated, key
created/rotated/revoked, each with the actor's id. **Never the raw key or its hash** — only
`Id`/`Name`/`KeyPrefix` (e.g. `stak_l6RwsH3...`, never enough to reconstruct or misuse).

## Diagnostics extension

`GET /api/admin/diagnostics` (Phase 58/60) gained an `apiKeys` section:

```json
{ "active": 3, "expiringSoon": 1, "revoked": 5, "expired": 0, "recentAuthFailures": 2 }
```

Counts only, `expiringSoon` = active keys expiring within 7 days, `recentAuthFailures` = invalid
`X-Api-Key` attempts across every source IP in the last hour (from `IApiKeyFailureTracker`). Never
a key value, prefix, or which specific key/IP was involved. Never affects the top-level
healthy/degraded/failing status — an expiring or failing key is an operational note for an admin,
not evidence the application itself is unhealthy.

## Migration / upgrade compatibility

One additive EF Core migration (`AddApiKeysAndServiceAccounts`): a new nullable-default
`Users.IsServiceAccount` column and a new `ApiKeys` table. No existing column changes, no data
migration needed. An existing Debian installation picks this up the same way every prior
schema-changing phase's migration has — `scripts/update-debian.sh`'s existing migration-apply
step, nothing new required.

## Testing

`scripts/test-api-keys.sh` — a live HTTP probe against a running instance (mirrors
`scripts/security-probe.sh`'s established pattern), creating a throwaway service account + key via
the real admin endpoints and asserting: valid key succeeds; missing/garbage/revoked keys all get a
generic 401; cookie-only auth still works unmodified; a service account can never log in via
password; rotation invalidates the old key and the new one works; a zero-project-membership
service account gets an empty list (never a 403); repeated invalid attempts all still return 401
(never a 5xx). Cleans up (deactivates) everything it creates regardless of pass/fail.

```bash
bash scripts/test-api-keys.sh
bash scripts/test-api-keys.sh --base-url=https://staging.example.com \
    --admin-email=admin@example.com --admin-password='...'
```

This is a bash script, not a new xUnit test project — this repo's "no automated test projects yet"
(see `CLAUDE.md`) is a standing, unchanged decision; every prior phase's own testing
(`test-upgrade-engine.sh`, `test-certify-release.sh`, `security-probe.sh`, `test-performance.sh`)
follows the same live-script convention.
