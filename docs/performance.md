# Performance, scalability & load testing (Phase 60)

This document covers the performance measurement architecture: what's actually measured, why, the
dataset/load profiles, targets, regression testing, and this phase's safety requirements. For
troubleshooting an actual slow/degraded instance, see
[docs/performance-runbook.md](performance-runbook.md).

## What was actually inspected before optimizing

Per this phase's own "do not optimize blindly — identify actual critical paths first," every
change below started from a direct audit of Phases 1–59's real implementation, not assumption:

- The task list (My Tasks, Sheet, Kanban, Calendar, Timeline, Gantt) loads **one unfiltered
  request per project** and does all filtering/sorting/search **client-side** — a decision
  `useTaskFilters.ts`'s own comment already flagged as "explicitly deferred to the not-yet-started
  performance phase once real scale requires it." Kanban/Calendar/Timeline/Gantt confirmed to have
  **no separate backend endpoints** — they're pure frontend layouts over that same response.
- `SearchService.SearchTasksAsync` had a confirmed N+1 (one project-role lookup per distinct
  project in a loop) — `SearchProjectsAsync` in the same file had already fixed the identical
  pattern; the fix just hadn't been applied to the tasks path. Fixed (Phase 60).
- `TaskItem` already had explicit indexes covering every filter/sort field this app actually
  queries by (`{ProjectId, SortOrder}` composite, `AssignedToUserId`, `Status`, `DueDate`,
  `Priority`) — confirmed by reading every `Data/Configurations/*.cs` file directly. No missing
  index was found for any real query pattern; none were added.
- `KanbanBoard.tsx`'s status grouping wasn't memoized (recomputed on every render, including every
  drag-over highlight) while Calendar/Timeline/Gantt's equivalent derived data all correctly were.
  Fixed. The Sheet view's own search input had no debounce (`GlobalSearch`'s did). Fixed.
- Zero row virtualization existed anywhere — a project with thousands of tasks rendered thousands
  of real `<tr>` elements. Added `@tanstack/react-virtual` (same vendor as the already-used
  `@tanstack/react-table`) to `TaskTable`/`AdminUsersTable`, **only above 100 rows** — a
  small/typical list renders exactly as it always has, unbounded height, whole-page scroll.

See the Phase 60 entry in README's Development-phases checklist for the full list of what changed.

## What this tooling measures

`scripts/test-performance.sh` times real HTTP requests against a running instance: login,
dashboard load, my-tasks load, project sheet load (and the Kanban/Calendar/Timeline/Gantt
equivalents — the same request, reported under each name since they share one backend endpoint),
task create/update/delete, search (common/rare/empty terms), filter (Search's
`status`/`priority`/etc. query params — the one place server-side filtering genuinely exists),
project switch, and the admin user list.

**A real, stated limitation, not glossed over**: the Sheet view's own filter/sort/search is
entirely client-side (see above) — there is no HTTP request to time for "how fast does filtering
100 already-loaded tasks run in the browser," because it never leaves the browser. This sandbox
has no browser-automation tool available in this whole project, so that specific number has never
been measured directly, here or in any earlier phase. The static-analysis review above (confirmed
memoization, confirmed row virtualization) is the best substitute available.

## Dataset profiles

`dotnet run --project Silver-Task.Server -- --perf-seed=small|medium|large` (Development-only,
mirrors `--seed`/`DemoDataSeeder`'s own gate) creates one project of clearly-synthetic data via
`PerformanceDataSeeder` — project names prefixed `[Perf Test]`, user emails on the reserved
`.invalid` TLD, so it's never mistaken for real data:

| Profile | Tasks | Users | Custom fields |
|---|---|---|---|
| SMALL | 100 | 10 | 2 (Text, Dropdown) |
| MEDIUM | 1,000 | 25 | 2 |
| LARGE | 5,000 | 50 | 2 |

`dotnet run --project Silver-Task.Server -- --perf-cleanup` removes every row the seeder could
have created (matched purely by the same project-name-prefix/email-domain markers), regardless of
what profile was seeded. Idempotent per profile — re-seeding an already-seeded size is a no-op;
run `--perf-cleanup` first to regenerate at a different size.

## Load profiles

| Profile | Concurrency (default) | Performs writes? |
|---|---|---|
| SMOKE | 1 | No — read-only |
| NORMAL | 5 | Yes |
| HEAVY | 20 | Yes |

`--concurrency=N` overrides the default for any profile — never hardcoded, per this phase's own
"do not hardcode unrealistic requirements." Concurrency is implemented via bash background jobs
(`curl ... & ... ; wait`), each simulating a distinct seeded perf-test user — this codebase's
established "no new external dependency" discipline (no k6/JMeter/Locust available or justified
here, same reasoning `certify-release.sh` and `security-probe.sh` already established for their
own live-orchestration needs).

**A confirmed, real interaction, not glossed over**: every simulated user's login in a NORMAL/
HEAVY concurrency pass originates from the one machine running the test, which
`Security:LoginRateLimit` (Phase 59, default 10 requests/60s, IP-partitioned) can't distinguish
from a single real client. Verified directly against a running instance (11 sequential logins from
one IP: the first 10 return `200`, the rest `429`). `test-performance.sh` reports a `429` on login
as `RATE-LIMIT`, tracked in its own `rateLimited` counter — never folded into `failures`, since
it's the security control working correctly, not an application defect. See
docs/performance-runbook.md for what to do about it.

## Performance targets

`scripts/perf-targets.conf` — plain `key=value`, the same parsing convention every env/config file
in this codebase already uses. Four bands: **FAST** ≤ fast_ms, **ACCEPTABLE** ≤ acceptable_ms,
**WARNING** ≤ warning_ms, **SLOW** above that. Some operations (project sheet, kanban, calendar,
timeline, gantt) have dataset-size-specific overrides, since "how long to load every task in a
project" is expected to scale with the actual task count — see the conf file's own header comment
for the full lookup order. **These are starting points, not measured-and-proven** — tune them from
your own real baseline numbers (`test-performance.sh` prints them every run) once you have some.

### Why a WARNING/SLOW result isn't a failure

`test-performance.sh` exits `0` for FAST/ACCEPTABLE/WARNING/SLOW alike — only a genuine request
failure or timeout (exit `3`) is treated as an error. A slow-but-completed request is real
information worth surfacing loudly, not the same category of problem as a request that didn't
complete at all.

## Test environment protection

`--target-env=development|test` is **required, with no default** — omitting it (or passing
anything else) prints exactly `PERFORMANCE TEST BLOCKED — TEST ENVIRONMENT NOT VERIFIED` and
exits immediately, before any request is sent. The NORMAL/HEAVY profiles (which perform writes)
additionally require a typed confirmation (`st_confirm_destructive`, the same mechanism rollback's
database restore and Phase 56's maintenance-window override already use) unless `--yes` is passed.

## Regression testing (tied to release certification)

`scripts/certify-release.sh --with-performance` runs `test-performance.sh` once against the live
baseline (right after it's confirmed healthy) and once against the live candidate (right after
it's confirmed healthy, before rollback), then compares key operation durations. A regression
(≥50% slower **and** at least 50ms of absolute difference, so trivial-data noise like 5ms→8ms never
reads as an alarming spike) is reported loudly but **never blocks certification by itself** — pass
`--fail-on-regression` too if you want a detected regression to block (exit `10`).

**A real limitation of this integration, stated plainly**: `certify-release.sh` installs in
Production mode (like a real deployment), and `PerformanceDataSeeder` is Development-only — no
dataset gets seeded as part of an automated certification run, so `--with-performance` there
measures against whatever data already exists (usually little to none). It still catches gross
regressions (an operation suddenly taking far longer even on trivial data), but it is **not** a
substitute for running `test-performance.sh` directly against a dev/test instance with a real
seeded dataset before a release.

## Reports

- **Per-run detailed report**: `$SILVERTASK_PERFORMANCE_DIR/performance-<dataset>-<profile>-<id>.jsonl`
  (default `/var/log/silver-task/performance/`) — one run-metadata line, one line per measured
  operation (operation, dataset size, duration, **response size**, success, verdict — never
  request bodies or task content), one summary line.
- **`--json`**: the same summary as structured JSON to stdout (version, environment, dataset,
  profile, warning/slow/failure/rate-limited counts, final result, report path) — for CI
  consumption.
- **Durable cross-version history**: `$SILVERTASK_PERFORMANCE_DIR/performance-history.jsonl` — one
  compact line per completed run (version, dataset, profile, final result, and only the *key*
  operation durations, not the full report) so version-over-version trends can be read without
  holding every raw measurement ever taken.

## What this phase deliberately did not do

Consistent with every earlier phase's scope discipline: no dedicated load-testing framework
(k6/JMeter/Locust — a new external dependency this project has never needed before), no APM/real
request-tracing integration (Phase 58's own "Monitoring" section already documents this boundary),
no server-side pagination/filtering rewrite of the core task-list views (row virtualization solves
the actual measured problem — unbounded DOM size — without an API-contract redesign; see the
Development-phases checklist entry for the full reasoning), no browser-based frontend timing (no
browser-automation tool available in this project's environment, stated plainly rather than
fabricated), and no resource-monitoring platform (CPU/memory/disk are the host's own job — see
`--doctor`/`--security-check` for the host-level checks this project already has).
