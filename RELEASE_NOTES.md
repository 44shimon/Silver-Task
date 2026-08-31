# Silver Task v1.0.0 — Release Notes

First stable release, covering everything built across Phases 1–48.

## Task management
- Projects containing tasks and subtasks, with drag-reordering, dependencies (finish-to-start and
  related types), and recurring task rules that auto-generate future occurrences.
- Full task detail: status, priority, assignee, dates, description, checklists, tags, and
  project-defined custom fields (EAV-based — new field types don't require a schema migration).

## Views
- **Sheet/Table** — the primary spreadsheet-style editable grid (sortable, filterable, resizable
  columns, inline editing).
- **Kanban**, **Calendar**, **Timeline**, and **Gantt** — all render the same underlying task data
  through different layouts; no separate data model per view.

## Search & organization
- **Global search** across tasks, projects, users, files, comments, tags, and templates, scoped to
  the caller's actual access (re-verified in Phase 47's security audit — no cross-project leakage).
- **Advanced filters** and **Saved Views** (personal and shareable) for repeatable filtered task
  lists.

## Collaboration
- **Comments** with @mentions, **file attachments** (authorized-download-only, never directly
  web-accessible), and a full **activity history** per task built by diffing changes inline (not a
  generic audit log).

## Automation & templates
- **Automations**: trigger → condition → action pipelines (task/project events), with an execution
  history and manual retry.
- **Project and task templates**, including instantiating a full project structure from a template
  in one action.

## Reporting
- **Dashboards** (personal workspace widgets) and **Reports** (overdue, workload, project
  progress, automation activity, and more), all built from batched/aggregated queries reviewed for
  N+1 issues in Phase 47.

## Notifications
- **In-app Notification Center** with read/unread state, per-category filtering, and mute controls.
- **Email notifications** reusing the same event pipeline as in-app notifications — never a
  separate "email detection" system — delivered through a background queue with retry (bounded,
  not infinite) and full delivery status tracking, admin-visible without exposing message content.
- **Daily and Weekly Digests**: per-notification-type delivery mode (Immediately / Daily Digest /
  Weekly Digest / Off), timezone-aware scheduling, duplicate-generation prevention verified across
  real app restarts, and content built from live, access-checked data (never a stale snapshot).
- **Admin-customizable email templates** (including the two digest templates) with a controlled,
  non-executing `{{Variable}}` substitution system — never arbitrary code/markup injection.

## Settings & administration
- **User settings**: profile, preferences (theme, timezone, date/time format, dashboard layout),
  notification delivery preferences, security (password change).
- **System settings**: organization-wide defaults and toggles across general, task defaults,
  security, behavior, attachments, and notification/email/digest configuration — all admin-only,
  all server-validated regardless of what the UI restricts client-side.
- **Roles and permissions**: system-wide roles (Administrator/Manager/Member) plus per-project
  roles (Manager/Member/Viewer) via a single shared authorization service used consistently across
  every resource type — audited in Phase 47 with no authorization gaps found.

## What's new in this release (Phase 45–48)
Email notifications, email templates, scheduled Daily/Weekly digests, and this production
deployment/release preparation work are all new since the last major documented milestone
(Notification Center, Phase 44). See `README.md`'s "Email notifications (Phase 45)" and
"Notification digests (Phase 46)" sections for full architectural detail.

## Known limitations
- **No automated test suite.** Correctness — especially the authorization discipline verified
  extensively in Phase 47's audit — currently relies on consistent hand-written patterns, not CI
  enforcement. This is the most significant gap for long-term maintainability and is explicitly
  the next phase this project should take on.
- **No horizontal scaling / multi-instance support.** Background job processing (email delivery,
  digests, automations, due-date sweeps) has no distributed locking — running more than one
  application instance would double-process background work. Deploy exactly one instance.
- **Performance is untested at real scale.** Verified against a modest seeded dataset (dozens of
  users, tens of projects, hundreds of tasks); several report/list endpoints load a full result set
  into memory before aggregating rather than pushing aggregation into SQL. Expected to be fine for
  a modest V1 launch; several concrete candidates for future optimization are listed in
  `README.md`'s "V1.0.0 release readiness" section.
- **No mobile-specific UI.** The SPA is responsive but has not been purpose-built or tested for
  small-screen/touch workflows.
- **No external integrations** (calendar sync, Slack/Teams, third-party SSO, public API/webhooks)
  — out of scope for this release.
- **No dedicated APM/metrics integration.** Health-check endpoints (`/api/health`,
  `/api/health/ready`) and structured logs are the extent of built-in observability; see
  `DEPLOYMENT.md`'s "Monitoring" section.
- **~910KB single-chunk frontend bundle** (234KB gzipped) — functional, but a candidate for
  route-level code-splitting in a future release.
- One **Medium** (dormant — see `README.md`) and several **Low** findings from the Phase 47 audit
  remain open by design; none are security-critical or user-facing.
