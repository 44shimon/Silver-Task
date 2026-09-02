# Production monitoring & diagnostics runbook (Phase 58)

**This document's scope was inferred, not taken from an original spec.** The real Phase 58 spec
was truncated the same way Phases 56/57's were, cutting mid-header straight into a trailing
success-checklist fragment, with all the actual numbered requirements missing. This plan was
inferred from the phase title, the one goal sentence that did come through ("create a production
monitoring and diagnostics foundation that allows administrators to quickly determine whether
Silver Task is healthy, degraded, or failing"), and the pattern established by Phases 51–57. If
this doesn't match what you actually asked for, say so.

## What this is

- **`GET /api/health`** and **`GET /api/health/ready`** — unchanged since Phase 48, anonymous,
  binary (up/down). Keep pointing your external uptime monitor (Uptime Robot, a Kubernetes/
  orchestrator probe, a cron + alert script) at these exactly as before — nothing here changes.
- **`GET /api/admin/diagnostics`** (new, Administrator-only) — a richer, three-state
  (`healthy`/`degraded`/`failing`) snapshot: database reachability *and* latency, attachment
  storage disk space, and a per-worker heartbeat for each of the 6 interval-driven background
  services. This is for an administrator actively investigating "is something wrong," not for an
  external uptime monitor (it requires an authenticated session, unlike the health endpoints).
- **`scripts/update-debian.sh --doctor`** (Phase 56) gained one more read-only check: is the
  currently-running application actually healthy right now (`GET /api/health/ready`, no
  credentials needed). Still `WARN`, never `FAIL` — an already-unhealthy app is exactly the kind
  of thing you might run `--doctor` to help diagnose, so it must not itself block the rest of the
  host-level preflight report.

No APM/metrics library is introduced (no OpenTelemetry/Prometheus exporter, etc.) — consistent
with DEPLOYMENT.md's existing "## Monitoring" section, which already documents that as real,
additional work not attempted in this codebase.

## The three states

| Status | Meaning |
|---|---|
| `healthy` | Everything checked is within normal bounds. |
| `degraded` | The app is still serving requests, but something needs attention before it becomes a real outage — slow database, low disk space, or a background worker that's stopped making progress. |
| `failing` | The database is unreachable. Currently the only condition that produces `failing` — everything else tops out at `degraded`, since the application process itself is still up and responding. |

The top-level `status` field is the worst of every individual check below it — read that one field
first, then drill into the section that's not `healthy` for the specific cause.

## Querying it

```bash
# Using the same cookie-based session your browser already has (log in first via the UI, or
# POST /api/auth/login), or any other authenticated Administrator session:
curl -s -b cookies.txt https://your-domain/api/admin/diagnostics | jq .
```

```json
{
  "status": "degraded",
  "version": "1.2.0",
  "timeUtc": "2026-03-05T02:14:07Z",
  "uptimeSeconds": 86412.3,
  "database": { "status": "healthy", "reachable": true, "latencyMs": 8 },
  "diskSpace": { "status": "degraded", "path": "/var/lib/silver-task/attachments", "freeBytes": 512000000, "totalBytes": 21474836480, "freePercent": 2.4 },
  "backgroundWorkers": [
    { "name": "email-delivery", "status": "healthy", "lastSuccessfulRunUtc": "2026-03-05T02:13:58Z", "intervalSeconds": 20 },
    { "name": "notification-retention", "status": "degraded", "lastSuccessfulRunUtc": "2026-03-04T00:10:00Z", "intervalSeconds": 86400 },
    { "name": "automation-queue", "status": "not-applicable", "lastSuccessfulRunUtc": null, "intervalSeconds": null }
  ]
}
```

A worker showing `"status": "starting"` with `lastSuccessfulRunUtc: null` simply hasn't completed
its first tick yet (normal for a few seconds right after a restart — every worker runs once
immediately on startup rather than waiting a full interval) — not a problem by itself. The one
worker always shown as `"not-applicable"` is `automation-queue`: it's purely event-driven (reacts
to automation triggers as they happen, no fixed polling interval), so "staleness relative to an
interval" doesn't mean anything for it — see AutomationQueueBackgroundService's own doc comment.

## Remediation by cause

**`database.status: failing`** — the app can't reach PostgreSQL at all.
1. `sudo systemctl status postgresql` — is it running?
2. Check `ConnectionStrings__DefaultConnection` in `/etc/silvertask/silvertask.env` still matches
   the actual host/port/database/credentials (`sudo -u postgres psql -l` to list databases).
3. `sudo ./scripts/update-debian.sh --doctor` — the same connectivity gap will also show up there.

**`database.status: degraded`** (reachable, but latency over `Diagnostics__DbLatencyDegradedMs`,
default 1000ms) — the connection works but is slow.
1. Check host CPU/memory/disk I/O pressure (`top`, `iostat`) — PostgreSQL contending for resources
   with something else on the same host is the most common cause.
2. Check PostgreSQL's own slow-query log if enabled.
3. If this is consistently slow rather than a one-off blip, consider whether the host is
   undersized for the current data volume.

**`diskSpace.status: degraded`** (free space under `Diagnostics__DiskFreePercentDegraded`, default
10%) — the attachments storage drive is running low.
1. `df -h` on the path reported in `diskSpace.path`.
2. Check `scripts/backup-debian.sh`'s retention isn't accumulating more backup sets than expected
   (see [Backup](../README.md#backup) in the README) if backups share the same drive.
3. Check for unusually large recent uploads via the app's own Admin → Files views.

**A background worker `status: degraded`** (no successful tick in longer than its own interval
times `Diagnostics__WorkerStaleMultiplier`, default 3x) — that specific worker has stopped making
progress.
1. `journalctl -u silvertask -n 200 | grep -i "<worker-related term>"` — every worker logs its own
   per-tick failures via `ILogger.LogError` before continuing (e.g. search for "Email delivery
   sweep failed", "Digest scheduler sweep failed" — see each `*BackgroundService.cs`'s own catch
   block for its exact log message).
2. A single transient failure self-heals on the next tick — persistent staleness means every tick
   is failing, most commonly a database problem (see above) since every worker needs a DB
   connection to do anything.
3. `sudo systemctl restart silvertask` restarts every worker along with the app itself — reasonable
   first response if the cause isn't immediately obvious from the logs, since a fresh process
   re-registers all heartbeats from zero.

## Limitations

- Reflects the state of exactly one running instance. This app has no multi-instance/distributed
  coordination anywhere (see README → "Background Workers") — running more than one copy means
  more than one independent diagnostics view, not a merged one.
- A point-in-time snapshot on each request, not a time series — there's no history of past
  `degraded`/`failing` periods stored anywhere. Ship `journalctl`/application logs to your own log
  aggregation if you need that.
- No email/webhook alerting is built in — wire your own external monitor to poll
  `GET /api/admin/diagnostics` (or the simpler anonymous `/api/health/ready` for basic uptime) and
  alert on its own schedule.
