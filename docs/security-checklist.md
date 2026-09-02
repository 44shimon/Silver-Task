# Security hardening & administrator checklist (Phase 59)

**This document's scope was inferred, not taken from an original spec.** The real Phase 59 spec
was truncated the same way Phases 56–58's were — this time a 15-area topic list and a partial
success checklist came through, but the actual numbered requirements didn't. This checklist was
built from that list plus a renewed, three-part audit of the actual Phases 1–58 implementation
(not just the original Phase 47 code). If this doesn't match what you actually asked for, say so.

This is one cohesive checklist covering all 15 named areas: what's automatically enforced by the
application/installer today, what requires one-time operator action, and what's an explicit,
documented limitation left to your own infrastructure. Run `sudo ./scripts/update-debian.sh
--security-check` for an automated pass over most of the file-permission/network-exposure items
below.

## Authentication

- Passwords hashed via ASP.NET Core Identity's `PasswordHasher` (auto-rehash on scheme upgrade).
- Per-account lockout after `Security.MaxFailedLoginAttempts` (default 5, admin-configurable),
  `Security.AccountLockoutDurationMinutes` (default 15).
- **New**: `POST /api/auth/login` is also rate-limited per client IP
  (`Security__LoginRateLimit__PermitLimit`/`WindowSeconds`, default 10/60s) — on top of, not
  instead of, the per-account lockout. Stops distributed spraying across many accounts from one
  source; the lockout alone couldn't.
- **Left to you**: SSH brute-force protection (fail2ban or equivalent) — outside this app's scope,
  it protects the host, not the application.

## Authorization / User Access / Administrator Access

- `ProjectAccessService`'s three tiers (participate/edit/manage) are used consistently by every
  project-scoped service — confirmed by direct code audit, not just Phase 47's original claim.
- The global `FallbackPolicy` requires authentication on anything without an explicit
  `[Authorize]`/`[AllowAnonymous]` — new controllers are unauthenticated-denied by default.
- `[AllowAnonymous]` inventory (confirmed narrow, all justified): login, health checks, first-user
  bootstrap (`POST /api/users`, only when zero users exist), public branding settings, and the SPA
  shell/static assets (must be loadable before a session exists).
- **New**: admin-sensitive actions (role change, activate/deactivate, password reset, deletion) now
  produce a structured log line (`Admin action: ...`) — see "Audit Events" below.

## API Protection

- CORS defaults to **no** cross-origin access until `Cors__AllowedOrigins__0` is explicitly set —
  ships empty in `appsettings.json`.
- `[RequestSizeLimit]` on upload endpoints; nginx's `client_max_body_size 100M` as a backstop.
- **New**: login rate limiting (see "Authentication" above).

## Session Security

- `silvertask_auth` cookie: `HttpOnly`, `SameSite=Strict`, `Secure` derived from the actual request
  scheme (correctly accounts for `ForwardedHeadersMiddleware` behind a reverse proxy).
- Session length via `Security.SessionTimeoutMinutes` (default 240 minutes, admin-configurable).
- **Known limitation, not fixed this phase**: no refresh-token/server-side revocation list — a
  copied JWT remains valid until natural expiry; `Logout` only clears the client-side cookie. If
  you suspect a token was compromised, the only mitigation today is shortening
  `Security.SessionTimeoutMinutes` and/or rotating `Jwt__Secret` (invalidates every session).

## Secrets Management

- DB password and JWT secret generated via `openssl rand -hex` at install time, never hardcoded,
  never logged (confirmed by direct grep of every script for places that write to a log file).
- `/etc/silvertask/silvertask.env`: `640`, `root:silvertask`.
- **Left to you**: periodic secret rotation is not automated — rotating `Jwt__Secret` invalidates
  every active session; rotating the DB password requires updating both PostgreSQL and the env
  file in the same maintenance window.
- **Left to you**: `rollback.sh`'s emergency `silvertask.env.emergency-<timestamp>` copies (Phase
  55) have correct `600` permissions but are never automatically cleaned up. Periodically remove
  old ones: `find /etc/silvertask -name 'silvertask.env.emergency-*' -mtime +30 -delete`.

## Configuration Security

- No secrets in `appsettings.json`/`appsettings.Development.json` (both ship empty placeholders).
- `.gitignore` blocks `.env*` (except `.env.example`) and `appsettings.*.local.json`.

## Database Access

- The app's PostgreSQL role owns only its own database — no superuser, no cross-database access.
- PostgreSQL is never opened in the firewall; Kestrel binds loopback-only; nginx is the only
  public-facing process.
- **Left to you**: `pg_hba.conf`/`postgresql.conf` are never touched by this project's scripts —
  hardening is inherited from the Debian `postgresql` package's stock defaults
  (`listen_addresses = localhost`), not asserted or pinned by the installer. `--security-check`
  verifies PostgreSQL isn't *currently* listening on a non-loopback address, but that's a runtime
  check, not a guarantee against future manual config drift.

## Deployment Security

- **New**: `deploy/silvertask.service` now includes systemd sandboxing (`ProtectSystem=strict`,
  `NoNewPrivileges`, `PrivateTmp`, `ProtectHome`, and more) on top of already running as the
  non-root `silvertask` user. `ReadWritePaths` deliberately covers the whole install directory, not
  just the attachments storage root — this app's full runtime write surface (e.g. ASP.NET Core's
  default Data Protection key persistence) couldn't be verified with confidence outside a real
  Debian host, so this is intentionally a little broader than the documented minimum. Tighten it
  further once you've confirmed exactly what your instance writes to (`journalctl` will show
  `Read-only file system` errors if `ReadWritePaths` is ever too narrow).

### Applying this to an existing installation

`--activate` never re-copies the systemd unit (only `install-debian.sh` does), so upgrading via
`update-debian.sh` does **not** silently apply this hardening. `--security-check` reports `WARN` if
your deployed unit predates it. To apply it manually:

```bash
sudo cp deploy/silvertask.service /tmp/silvertask.service.new
# Fill in the same substitutions install-debian.sh makes automatically (WorkingDirectory,
# ExecStart, User, EnvironmentFile, ReadWritePaths) to match your actual installation, then:
sudo cp /tmp/silvertask.service.new /etc/systemd/system/silvertask.service
sudo systemctl daemon-reload
sudo systemctl restart silvertask
sudo ./scripts/update-debian.sh --security-check   # confirm it now reports PASS
```

- Firewall (`ufw`): OpenSSH + only the HTTP/HTTPS ports actually in use.
- **Left to you**: nginx's TLS configuration relies on certbot's/nginx's compiled-in defaults —
  no explicit `ssl_protocols`/`ssl_ciphers` pinning. Reasonable for most deployments; revisit if
  your compliance requirements need an explicit cipher list.

## Input Validation

- `[ApiController]` + data annotations on every DTO (32 controllers, confirmed via audit).
- The EAV custom-field validator (`CustomFieldValueValidator`) rejects non-`http(s)` URL schemes
  on Link fields (blocks `javascript:`/`data:`).
- No stored-XSS path found: the frontend never uses `dangerouslySetInnerHTML` for user content
  (confirmed by repo-wide search), relying on React's default escaping rather than backend
  sanitization — a defense-in-depth gap, not a currently-exploitable one. The new
  `Content-Security-Policy` (below) is an additional independent layer on top of this.

## Security Headers

- **New**: every response now carries `Content-Security-Policy: default-src 'self'; ...`,
  `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`, and (Production only) HSTS. Verified live
  against a real response; **could not be verified against a real browser's DevTools console** —
  no browser-automation tool is available in this project's development environment. If you
  observe a CSP violation in production, it means genuinely new frontend behavior (a new external
  resource, an inline script) was introduced since this was last verified — check the browser
  console and adjust `SecurityHeadersMiddleware.cs`'s policy deliberately, don't just widen it to
  `'unsafe-inline'`/`*` without understanding why it's needed.

## Audit Events

- **New**: `UserService` now logs (`ILogger`, structured, `Admin action: ...` prefix) role changes,
  activation/deactivation, password resets, and deletions, with both the target user and the
  caller's ID. Same mechanism this app already documents as its audit trail — ship
  `journalctl -u silvertask` (or stdout/stderr) to whatever log aggregation you run.
- `AuthService` already logged failed logins/lockouts (`LogWarning`) before this phase.
- **Known limitation, not fixed this phase**: this is a log stream, not a queryable audit-log
  table — there's no admin UI to browse "who did what," only `grep`/log aggregation.

## Dependency Review

- **New**: `scripts/check-dependencies.sh` wraps `dotnet list package --vulnerable
  --include-transitive` and `npm audit`. Dev/CI-oriented — deliberately **not** part of
  `--security-check` or `--doctor`, since a hardened production host may intentionally block
  egress to the NuGet/npm registries this needs, and a vulnerability scan has no business running
  on every deploy anyway. Run it before tagging a release, alongside `certify-release.sh`.
- As of this phase, both ecosystems are clean: `dotnet list package --vulnerable
  --include-transitive` and `npm audit` both report zero known vulnerabilities.

## Security Testing

- **New**: `scripts/security-probe.sh` — a repeatable, scripted version of Phase 47's one-time
  manual audit. Probes, against a running instance: unauthenticated access to a protected endpoint
  (expects `401`), a Member session against an admin-only endpoint (expects `403`), an IDOR probe
  (a Member requesting a project they don't belong to — expects `403`/`404`, never `200`), and that
  the new security headers are present. Uses the seeded demo accounts by default
  (`--seed`/`Development`-gated) or `--admin-email=`/`--member-email=`/etc. against any instance.
- **New**: `--security-check` (mirrors `--doctor`) — file permissions, firewall, PostgreSQL
  exposure, systemd hardening, response headers. Exit `38` on any `FAIL`.

## What this phase deliberately did not do

Consistent with every earlier phase's scope discipline: no 2FA/MFA, no CSRF token scheme (the
cookie is `SameSite=Strict` plus JWT-in-cookie, not the classic session-cookie CSRF-prone pattern —
re-evaluate if `SameSite` is ever relaxed), no WAF/DDoS mitigation (that's infrastructure/CDN-layer,
out of scope for an application-level phase), no queryable audit-log UI (log-stream only, see
"Audit Events" above), no automated dependency-update PRs (Dependabot/Renovate — a GitHub-hosting
concern, not something this codebase's own scripts should manage), and no changes to
`pg_hba.conf`/PostgreSQL's own configuration (relies on Debian's stock defaults, checked but not
asserted by `--security-check`).
