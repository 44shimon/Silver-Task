# Rollback operator checklist

A concise run-through for rolling back a failed, interrupted, or unwanted upgrade. See
[rollback.md](rollback.md) for the full explanation of each step, and its warning about database
restore discarding data created after the pre-upgrade backup.

```text
[ ] Review the upgrade failure
    sudo journalctl -u silvertask -n 200
    cat /var/log/silver-task/upgrade.log   (tail the most recent attempt)

[ ] Check current application status
    sudo systemctl status silvertask
    curl -f http://127.0.0.1:5000/api/health

[ ] Review rollback status and eligibility
    sudo ./scripts/update-debian.sh --status
    — confirms there's a previous release + backup to roll back to, and whether the last
      activation actually switched the release (nothing to roll back if it never did)

[ ] Review available backups (the pre-upgrade backup rollback will use, and free space for
    the emergency backup rollback will create before any database restore)
    ls -la /var/backups/silver-task/
    df -h /var/backups

[ ] Confirm the rollback target — --status shows it, or read the manifest directly:
    cat /var/backups/silver-task/<timestamp>/manifest.json

[ ] Review whether database restore will be required (decision is derived from the failed
    upgrade's own recorded migrationRequired flag — never guessed)
    sudo ./scripts/update-debian.sh --rollback --dry-run
    — shows "Database Restore Decision: APPLICATION_ONLY_ROLLBACK" or
      "DATABASE_RESTORE_REQUIRED" (or blocks with MANUAL_RECOVERY_REQUIRED)

[ ] Run the rollback dry-run and review the full plan
    sudo ./scripts/update-debian.sh --rollback --dry-run

[ ] Confirm the rollback plan and execute
    sudo ./scripts/update-debian.sh --rollback [--reason="..."] [--restore-config]
    — read the confirmation panel (current/target version, backup verification, database
      restore decision) before answering [y/N]; if a database restore is required, a SECOND
      prompt requires typing the exact target version to confirm

[ ] Watch it run — do not walk away mid-rollback
    journalctl -u silvertask -f            (in a second terminal, during rollback)

[ ] Verify health
    curl -f http://127.0.0.1:5000/api/health

[ ] Verify version matches the rollback target
    curl -s http://127.0.0.1:5000/api/health | grep version

[ ] Verify application availability from outside the host (through the reverse proxy)
    curl -f https://<your-domain>/api/health

[ ] Review final rollback status
    sudo ./scripts/update-debian.sh --status
    — should report "Last Rollback: COMPLETED" with Database Restored / Configuration
      Restored / Application Health all shown accurately

[ ] If anything failed: do NOT retry blindly. Read the recovery information block it
    printed, check /var/log/silver-task/upgrade.log, and consult rollback.md "Failure
    handling" / "Manual recovery" first.
```

### What this checklist deliberately does not include

- **Automatic retry** — a failed rollback requires administrator review, not a scripted re-attempt.
- **Restoring the emergency backup automatically** — it exists so you *can* recover the
  pre-rollback data if needed, but doing so is always a separate, manual, deliberate decision (see
  [restore.md](restore.md)).
- **Skipping the dry run** — always worth it before a rollback that will restore the database.
