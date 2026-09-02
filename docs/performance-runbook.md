# Performance troubleshooting runbook (Phase 60)

Safe, non-destructive first-response procedures for a slow or degraded instance. See
[docs/performance.md](performance.md) for the full measurement architecture, and
[docs/monitoring-runbook.md](monitoring-runbook.md) for the broader healthy/degraded/failing model
(Phase 58) this runbook complements.

## Slow task sheet (Sheet/Kanban/Calendar/Timeline/Gantt)

All five share one backend request (`GET /api/projects/{id}/tasks`) — confirmed by direct code
audit, no separate endpoints exist for the other four.

1. `sudo ./scripts/test-performance.sh --target-env=development --dataset=<size matching the real
   project's rough task count> --profile=smoke` — check the `project_sheet` line's verdict and
   response size.
2. If the request itself is slow (not just the browser rendering it): check
   `GET /api/admin/diagnostics` → `database.latencyMs`. A slow query usually means a slow
   database, not a slow endpoint — see "Slow database" below.
3. If the request is fast but the *page* still feels slow: row virtualization only activates above
   100 tasks (`TaskTable.tsx`'s `VIRTUALIZATION_ROW_THRESHOLD`) — a project with more tasks than
   that should already be windowed. Confirm the project's actual task count; if it's well under
   100, this is a different kind of problem (browser-side), not something this project's own
   backend tooling can diagnose without a browser-automation tool (not available here — see
   docs/performance.md's own stated limitation).

## Slow search

1. `sudo ./scripts/test-performance.sh --target-env=development --profile=smoke` — check
   `search_common`/`search_rare`/`search_empty`/`filter`.
2. `SearchService` uses PostgreSQL `ILIKE` (no full-text index by design — see the class's own doc
   comment; this app's per-user working set was judged not to justify one). A `search_common`
   result that's dramatically slower than `search_rare` is expected (more candidate rows to score)
   — a `search_empty` result that's slow is more suspicious, since there's nothing to match.
3. Check for an unusually large number of distinct projects the caller has access to — the
   project-role batching fix (Phase 60) removed the N+1 there, but a caller who's a member of an
   unusually large number of projects still does more work than a typical caller.

## Slow dashboard

1. Dashboard makes several independent requests per widget (Team Workload, Notifications, Recent
   Activity, Admin Overview, Reports Summary, Workflow, Recent Files — confirmed by direct audit,
   not a single aggregated call) — a `GET /api/dashboard`-only measurement doesn't capture the
   other widgets. `test-performance.sh`'s `dashboard` operation measures the main aggregated
   endpoint only.
2. `GET /api/admin/diagnostics` → `recentSlowOperations` — look for `Dashboard.*` entries; the
   operation label tells you which specific dashboard action was slow, without needing to guess.

## Slow API generally

1. `GET /api/admin/diagnostics` → `recentSlowOperations` (Phase 60) — the most recent requests at
   or above `Diagnostics:SlowOperationThresholdMs` (default 1000ms), newest first, with the
   specific controller/action and duration. Never the request body or query string.
2. Cross-reference the operation name against `scripts/perf-targets.conf` if you want to know
   whether it's actually outside the expected range for its category, or just naturally heavier.

## Slow database / high database latency

1. `GET /api/admin/diagnostics` → `database.status`/`database.latencyMs`.
2. Check host CPU/memory/disk I/O pressure on the database server (`top`, `iostat`) — contention
   with something else on the same host is the most common cause.
3. Check PostgreSQL's own slow-query log if enabled.
4. **Do not** add new indexes speculatively — this phase's own audit found every current query
   pattern already covered; if a *new* query pattern is now slow, confirm which one specifically
   (via `recentSlowOperations` or the database's own slow-query log) before adding anything, per
   this project's own "every index should have a reason based on query behavior."

## High memory / high CPU

This project doesn't ship a resource-monitoring platform (by design — see docs/performance.md's
own "what this phase deliberately did not do") and this sandbox has no long-running host to
observe growth on. Use your host's own tooling (`top`, `htop`, `ps`) alongside
`systemctl status silvertask`; a steadily climbing memory figure across many hours with otherwise
steady traffic is the signal worth investigating, not a single high reading.

## Database connection pressure

1. `AppDbContext` is registered via plain `AddDbContext` (scoped, one instance per request) — no
   manual connection creation exists anywhere in `Services/` (confirmed by repo-wide grep), so
   connection exhaustion would come from Npgsql's own pool sizing under real concurrent load, not
   a connection leak in this codebase.
2. Run `sudo ./scripts/test-performance.sh --target-env=development --profile=heavy` against a
   disposable/test instance to see whether failures appear under real concurrency before assuming
   a leak — a `heavy`-profile failure that doesn't reproduce at `normal` concurrency points at pool
   sizing, not application code.

## Rate-limited logins during a load test (`RATE-LIMIT` lines, `rateLimited` count)

`test-performance.sh`'s NORMAL/HEAVY profiles log in as several distinct seeded perf-test users at
once — but every one of those logins originates from the one machine running the test, which
`Security:LoginRateLimit` (Phase 59, IP-partitioned, default 10 requests/60s) can't distinguish
from a single real client. This is confirmed, reproducible behavior (verified directly: 11
sequential login attempts from one IP against a fresh instance return `200` for the first 10 and
`429` for the rest), not a script bug or an application defect — the rate limiter is doing exactly
what it's for.

`test-performance.sh` reports this distinctly (`RATE-LIMIT <operation>`) and tracks it in its own
`rateLimited` counter, separate from `failures` — a rate-limited login never flips `FINAL RESULT`
to `FAILED` or the exit code to `3`, since it isn't evidence the *application* is broken.

If you see a lot of these:
1. Wait for the 60-second window to reset before re-running, or space out repeated runs.
2. Reduce `--concurrency` so the sequential pass's own logins (user + admin) plus the concurrency
   pass's logins together stay under the budget.
3. On a disposable/test target only, raise `Security__LoginRateLimit__PermitLimit`/
   `WindowSeconds` for the duration of the load test — never on production, and never as a
   permanent change made just to silence this. See `deploy/silvertask.env.example`.

## Performance regression after an upgrade

1. `sudo ./scripts/certify-release.sh --candidate=X.Y.Z --with-performance
   --disposable-host-confirmed` (on a disposable host) reports any regression between the previous
   and new version directly.
2. Check `$SILVERTASK_PERFORMANCE_DIR/performance-history.jsonl` for the version-over-version
   trend — one line per completed run, most recent last.
3. **Do not** roll back purely because of a performance regression without checking whether it's
   also a correctness problem first — see [docs/rollback.md](rollback.md) for the actual rollback
   decision/procedure; a regression alone doesn't necessarily mean the upgrade itself is broken.
