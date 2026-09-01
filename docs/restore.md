# Restore procedures

This document covers two different restore scenarios:

1. **Restoring into an isolated test database** — the safe, recommended way to *verify* a backup
   is actually good (including every pre-upgrade backup the [upgrade
   engine](../README.md#upgrade-engine) creates), without any risk to production.
2. **Restoring over a live production installation** — a rare, high-stakes, manual procedure. The
   full, Phase-47-verified command sequence for this already lives in the main README →
   [Restore](../README.md#restore); this document does not duplicate it. Read the warning in that
   section before ever running those commands.

**Never restore directly over production as a first step.** If you're unsure whether a backup is
good, restore it into an isolated test database first (procedure below) and verify it there.

---

## Finding the right backup

Every backup set lives under `$SILVERTASK_BACKUP_DIR` (default `/var/backups/silver-task/<UTC
timestamp>/`) and — since Phase 53 — contains a `manifest.json` describing what's actually in it:

```bash
sudo cat /var/backups/silver-task/<timestamp>/manifest.json
```

```json
{
  "type": "pre-upgrade",
  "createdAt": "2026-09-01T12:00:00Z",
  "upgradeId": "upgrade-20260901-120000-a1b2c3",
  "installedVersion": "1.0.1",
  "targetVersion": "1.1.0",
  "databaseBackup": "database.dump",
  "databaseBackupVerified": true,
  "attachmentsBackup": "attachments.tar.gz",
  "configurationBackup": "silvertask.env",
  "configurationBackupVerified": true
}
```

- `"type"` is `"pre-upgrade"` for a backup the upgrade engine created automatically before staging
  a release, or `"manual"` for one you ran yourself (`sudo ./scripts/backup-debian.sh`) or a
  scheduled cron backup.
- `"upgradeId"` links a `pre-upgrade` backup to a specific attempt — cross-reference it against
  `$SILVERTASK_INSTALL_DIR/upgrade-state.json` (or `sudo ./scripts/update-debian.sh --status`) to
  see what that upgrade attempt's outcome was.
- `"databaseBackupVerified"`/`"configurationBackupVerified"` being `true` means
  `scripts/backup-debian.sh` itself already confirmed the backup is structurally readable (see
  below) — but that's not a substitute for actually restoring and checking the data, which is what
  this document is for.
- The manifest never contains passwords, connection strings, or any other secret value — it's safe
  to read, copy, or paste into a support request without redacting anything.

If you just want the most recent backup regardless of type: the newest timestamped directory under
`$SILVERTASK_BACKUP_DIR` is never deleted by retention (see README → "Upgrade Engine" →
"backup retention").

---

## Restoring the database into an isolated test database

This is the safe default — it never touches the production database, and is exactly the procedure
you should run to actually *trust* a backup rather than just seeing `databaseBackupVerified: true`
(that check only confirms `pg_restore` can read the file's table of contents, not that a full
restore succeeds or that the data inside is what you expect).

**Prerequisites**: a PostgreSQL server you can create a scratch database on (the same server the
production database runs on is fine — a differently-named database is still fully isolated from
production; nothing here reads or writes the production database).

```bash
# 1. Create a throwaway database — pick a name that can't be confused with production.
sudo -u postgres createdb silvertask_restore_test

# 2. Restore the backup into it.
sudo -u postgres pg_restore --no-owner --role=silvertask_app \
    -d silvertask_restore_test \
    /var/backups/silver-task/<timestamp>/database.dump

# 3. Verify: connect and spot-check.
sudo -u postgres psql -d silvertask_restore_test -c '\dt'                     # tables exist
sudo -u postgres psql -d silvertask_restore_test -c 'SELECT COUNT(*) FROM "Tasks";'
sudo -u postgres psql -d silvertask_restore_test -c 'SELECT COUNT(*) FROM "Users";'
sudo -u postgres psql -d silvertask_restore_test -c 'SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;'

# 4. Clean up when done.
sudo -u postgres dropdb silvertask_restore_test
```

What "good" looks like: `\dt` lists the full Silver Task schema (`Tasks`, `Projects`, `Users`,
`TaskComments`, `TaskActivities`, `TaskAttachments`, `CustomFields`, `CustomFieldOptions`,
`TaskCustomValues`, `ProjectMembers`, `__EFMigrationsHistory` — see README → "Database schema"),
the row counts are non-zero and roughly match what you'd expect from the live system at backup
time, and `__EFMigrationsHistory`'s most recent entries match the migrations you'd expect for the
version the backup was taken from (`"installedVersion"` in the manifest).

If you want to point a real (test/dev) instance of the application at the restored database rather
than just querying it directly, set `ConnectionStrings__DefaultConnection` in a **separate**
`silvertask.env`-style file pointing at `silvertask_restore_test`, and run the server with
`ASPNETCORE_ENVIRONMENT=Development` against it — never reuse the production env file for this.

---

## Configuration restore (isolated verification)

```bash
# Extract to a scratch location — never directly over /etc/silvertask/silvertask.env as a first step.
mkdir -p /tmp/silvertask-config-check
cp /var/backups/silver-task/<timestamp>/silvertask.env /tmp/silvertask-config-check/
chmod 600 /tmp/silvertask-config-check/silvertask.env

# Verify the expected keys are present WITHOUT printing their values:
grep -q '^ConnectionStrings__DefaultConnection=' /tmp/silvertask-config-check/silvertask.env && echo "connection string: present"
grep -q '^Jwt__Secret='                            /tmp/silvertask-config-check/silvertask.env && echo "JWT secret: present"

# Clean up — this copy still contains real secrets.
shred -u /tmp/silvertask-config-check/silvertask.env 2>/dev/null || rm -f /tmp/silvertask-config-check/silvertask.env
rmdir /tmp/silvertask-config-check
```

Never `cat`, `echo`, or otherwise print the file's contents to a terminal you don't control (screen
share, CI log, support ticket) — it contains the database password and JWT signing secret in
plaintext. The `grep -q` presence checks above are deliberately silent about values.

Only restore configuration **over** `/etc/silvertask/silvertask.env` if the live file was actually
lost (disk failure, accidental deletion) — restoring it over a currently-working file reverts to
old secrets and can break anything that already rotated a credential since that backup.

---

## Safety warnings

- **Never run a restore command against the production database as your first attempt at
  verifying a backup.** Use the isolated procedure above.
- A restore **overwrites** whatever is in the target database — always confirm you're pointed at
  `silvertask_restore_test` (or equivalent), not the production database name from
  `/etc/silvertask/silvertask.env`, before running `pg_restore`.
- If you do need to restore into production (disaster recovery), follow README → "Restore" exactly
  as written, including its own warning about taking a fresh backup of the *current* (possibly
  broken) state first, in case you need to get back to it.
- `pg_restore`/`createdb`/`dropdb` all require the `postgres` OS user (peer authentication, same as
  every other database-touching command in this project's scripts) — always run them via
  `sudo -u postgres`, never with the application's own `silvertask_app` credentials.
