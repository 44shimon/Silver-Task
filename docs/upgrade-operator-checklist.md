# Upgrade operator checklist

A concise run-through for performing a production upgrade. See
[upgrade-activation.md](upgrade-activation.md) for the full explanation of each step.

```text
[ ] Check the current installed/running version
    sudo ./scripts/update-debian.sh --status

[ ] Check whether an update is available
    sudo ./scripts/update-debian.sh --check

[ ] Review what a prepare would do, without changing anything
    sudo ./scripts/update-debian.sh --dry-run --latest
    (or: sudo ./scripts/update-debian.sh --dry-run --target-version X.Y.Z)

[ ] Verify backup capacity ahead of time
    df -h /var/backups   (or wherever SILVERTASK_BACKUP_DIR points)
    — the prepare step below re-checks this itself and blocks (exit 8) if insufficient, but
      confirming ahead of a maintenance window avoids a surprise abort mid-way

[ ] Review the target version's release notes / changelog before proceeding

[ ] Prepare the upgrade — creates + verifies a full backup, checks persistent-data
    placement, validates/plans any migration
    sudo ./scripts/update-debian.sh --latest
    (or: sudo ./scripts/update-debian.sh --target-version X.Y.Z)
    — confirm it reports READY FOR ACTIVATION before continuing; if it doesn't, stop and
      investigate (see the specific exit code / failure message)

[ ] Confirm the prepared upgrade before activating
    sudo ./scripts/update-debian.sh --status
    — check "Migration validation", "Migration plan", "Backup" all show OK

[ ] Activate — this is the step that actually changes anything. The application will be
    briefly unavailable (maintenance mode + a service restart + any migration).
    sudo ./scripts/update-debian.sh --activate
    — read the confirmation panel (current/target version, backup verification, whether a
      migration is required) before answering the [y/N] prompt

[ ] Watch it run — do not walk away mid-activation
    journalctl -u silvertask -f            (in a second terminal, during activation)

[ ] Verify health checks and version passed (the command itself reports this, but confirm)
    curl -f http://127.0.0.1:5000/api/health

[ ] Verify application availability from outside the host (through the reverse proxy)
    curl -f https://<your-domain>/api/health

[ ] Review final upgrade status
    sudo ./scripts/update-debian.sh --status
    — should report "Upgrade Status: COMPLETED" with Health Check / Version Validation /
      Smoke Tests all PASSED

[ ] If anything failed: do NOT re-run --activate blindly. Read the recovery information
    block it printed, check /var/log/silver-task/upgrade.log, and consult
    upgrade-activation.md "Failure handling" / "Interrupted upgrade detection" first.
```

### What this checklist deliberately does not include

- **Automatic rollback** — not implemented yet. Recovering from a failed activation is a manual
  procedure (see [restore.md](restore.md)), not a scripted step.
- **A web UI step** — there isn't one, by design; upgrade administration is CLI-only and requires
  root/sudo on the host, never an unauthenticated (or even authenticated) HTTP endpoint.
- **Skipping the dry run** — always worth the 30 seconds it takes, especially before activating
  against a target version you haven't prepared before.
