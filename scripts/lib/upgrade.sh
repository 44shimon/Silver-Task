#!/bin/bash
# Silver Task — Phase 52 upgrade engine primitives, used by scripts/update-debian.sh's
# --check/--status/--latest/--target-version/--dry-run modes.
#
# Sourced after lib/common.sh (relies on st_info/st_warn/st_error/st_json_escape and the
# SILVERTASK_* location constants — including the SILVERTASK_UPGRADE_* ones common.sh defines
# specifically for this file). Never sourced or executed directly.
#
# Deliberately separate from lib/common.sh: common.sh's helpers are shared by all four
# operator-facing scripts (install/update/backup/uninstall); everything here is only ever used by
# update-debian.sh's new upgrade-engine flags, not by the legacy full-update path or by any other
# script — keeping it in its own file means reading/reviewing "what does the upgrade engine
# actually do" never requires wading through backup/uninstall logic that has nothing to do with it.
#
# No `jq`/external JSON library dependency, matching every other script's convention (see
# lib/common.sh's st_json_escape doc comment) — every JSON file this engine reads or writes has a
# small, fixed, single-line-per-field shape, parsed/produced with grep/sed.

# --- Semantic versioning — Phase 52 supports stable MAJOR.MINOR.PATCH releases only ---

st_up_semver_valid() {
    [[ "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
}

# Prints -1, 0, or 1 to stdout (A<B, A==B, A>B). Both arguments must already be valid semver
# (callers check st_up_semver_valid first) — this does no validation of its own.
st_up_semver_compare() {
    local a_major a_minor a_patch b_major b_minor b_patch
    IFS=. read -r a_major a_minor a_patch <<< "$1"
    IFS=. read -r b_major b_minor b_patch <<< "$2"
    if [ "$a_major" -ne "$b_major" ]; then
        [ "$a_major" -lt "$b_major" ] && echo -1 || echo 1
        return
    fi
    if [ "$a_minor" -ne "$b_minor" ]; then
        [ "$a_minor" -lt "$b_minor" ] && echo -1 || echo 1
        return
    fi
    if [ "$a_patch" -ne "$b_patch" ]; then
        [ "$a_patch" -lt "$b_patch" ] && echo -1 || echo 1
        return
    fi
    echo 0
}

# Reads one version per line on stdin, writes them sorted ascending. Numeric per-component
# (`sort -t. -k*n`), not lexicographic — a plain `sort` would put "1.10.0" before "1.9.0".
st_up_semver_sort() {
    sort -t. -k1,1n -k2,2n -k3,3n
}

# --- Release discovery ---

# Lists every stable release tag on the configured remote (SILVERTASK_REPO_URL — never a URL
# supplied any other way; the brief's own "do not accept arbitrary URLs from command-line input"),
# sorted ascending. Pre-release tags (v1.1.0-beta, v1.1.0-rc1), peeled annotated-tag lines
# (refs/tags/v1.0.0^{}), and branch-like refs (main, latest, development) are never printed — they
# simply cannot match the strict "refs/tags/v<digits>.<digits>.<digits>" pattern below, so nothing
# extra is needed to exclude them.
#
# Returns 1 (nothing printed) if the remote itself is unreachable — distinct from "reachable but
# zero stable tags exist" (prints nothing, returns 0). Callers must check the exit status, not
# just emptiness, to tell "can't check" apart from "nothing to offer."
# Reads raw `git ls-remote --tags` output on stdin (tab-separated "<sha>\trefs/tags/<name>" lines)
# and prints only the stable version numbers, sorted ascending. Split out from
# st_up_discover_stable_releases so this parsing/filtering logic is independently testable without
# a real remote (see scripts/test-upgrade-engine.sh).
st_up_filter_stable_tags() {
    awk '{print $2}' \
        | sed -n 's#^refs/tags/v\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)$#\1#p' \
        | sort -u \
        | st_up_semver_sort
}

st_up_discover_stable_releases() {
    local raw
    raw="$(git ls-remote --tags "$SILVERTASK_REPO_URL" 2>/dev/null)" || return 1
    [ -n "$raw" ] || return 0
    printf '%s\n' "$raw" | st_up_filter_stable_tags
}

st_up_latest_stable() {
    st_up_discover_stable_releases | tail -1
}

# --- Version detection & consistency ---

# Prints the "version" field of $SILVERTASK_INSTALL_DIR/installed-version.json (Phase 51). Returns
# 1 (prints nothing) if the file doesn't exist — an install predating Phase 51, or no install at
# all. Callers must treat that as "unknown," never proceed as if it were consistent.
st_up_installed_version() {
    local file="$SILVERTASK_INSTALL_DIR/installed-version.json"
    [ -f "$file" ] || return 1
    sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$file" | head -1
}

st_up_installed_commit() {
    local file="$SILVERTASK_INSTALL_DIR/installed-version.json"
    [ -f "$file" ] || return 1
    sed -n 's/.*"gitCommit"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$file" | head -1
}

# Prints the running instance's own reported version via GET /api/health (Phase 51). Returns 1 if
# the app can't be reached at all — distinct from a version mismatch, so a down/starting app is
# never misreported as "wrong version."
st_up_running_version() {
    local base_url="${1:-http://127.0.0.1:5000}"
    local body
    body="$(curl -fsS --max-time 5 "$base_url/api/health" 2>/dev/null)" || return 1
    printf '%s' "$body" | sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1
}

# Sets ST_UP_CONSISTENCY to CONSISTENT / MISMATCH / UNKNOWN, and ST_UP_INSTALLED/ST_UP_RUNNING to
# whatever was actually read (empty string if unreadable). UNKNOWN — never MISMATCH — whenever
# either side can't be determined: an unreachable app or a fresh install with no
# installed-version.json yet is a "can't tell" state, not evidence of drift.
st_up_version_consistency() {
    ST_UP_INSTALLED="$(st_up_installed_version || true)"
    ST_UP_RUNNING="$(st_up_running_version || true)"
    if [ -z "$ST_UP_INSTALLED" ] || [ -z "$ST_UP_RUNNING" ]; then
        ST_UP_CONSISTENCY="UNKNOWN"
    elif [ "$ST_UP_INSTALLED" = "$ST_UP_RUNNING" ]; then
        ST_UP_CONSISTENCY="CONSISTENT"
    else
        ST_UP_CONSISTENCY="MISMATCH"
    fi
}

# --- Upgrade lock ---
#
# flock-backed: the lock is tied to an open file descriptor's lifetime, so a killed/crashed
# process releases it automatically at the OS level the moment it dies — no PID-liveness
# heuristic is needed for the actual "only one upgrade runs at a time" guarantee, and a crash can
# never permanently wedge future upgrades. The lock file also carries small metadata (PID/target/
# start time) purely for human-readable reporting (--status, "busy" error messages); that
# metadata is NOT what enforces exclusivity — flock is.
#
# ST_UP_LOCK_FD is allocated by st_up_lock_acquire (bash's `exec {VAR}>file` fd-allocation form,
# bash 4.1+; this repo's scripts already require a modern bash/coreutils Debian target).

st_up_lock_acquire() {
    local target_version="$1"
    exec {ST_UP_LOCK_FD}>"$SILVERTASK_UPGRADE_LOCK_FILE"
    if ! flock -n "$ST_UP_LOCK_FD"; then
        exec {ST_UP_LOCK_FD}>&-
        unset ST_UP_LOCK_FD
        return 1
    fi
    chmod 600 "$SILVERTASK_UPGRADE_LOCK_FILE" 2>/dev/null || true
    {
        printf 'pid=%s\n' "$$"
        printf 'targetVersion=%s\n' "$target_version"
        printf 'startedAtUtc=%s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    } >&"$ST_UP_LOCK_FD"
    return 0
}

st_up_lock_release() {
    [ -n "${ST_UP_LOCK_FD:-}" ] || return 0
    flock -u "$ST_UP_LOCK_FD" 2>/dev/null || true
    exec {ST_UP_LOCK_FD}>&- 2>/dev/null || true
    unset ST_UP_LOCK_FD
}

# Reports whether an upgrade is actually in progress right now, authoritatively — a fresh
# non-blocking flock attempt on the same file, not a trust-the-metadata's-PID check (a PID can be
# reused by an unrelated process after a crash). Sets ST_UP_LOCK_ACTIVE=true/false and, whenever a
# lock file with metadata exists on disk (even one nobody currently holds — the leftover evidence
# of an interrupted attempt), ST_UP_LOCK_PID/ST_UP_LOCK_TARGET/ST_UP_LOCK_STARTED. Returns 0 if
# idle, 1 if in progress.
st_up_lock_probe() {
    ST_UP_LOCK_ACTIVE=false
    ST_UP_LOCK_PID=""; ST_UP_LOCK_TARGET=""; ST_UP_LOCK_STARTED=""
    [ -f "$SILVERTASK_UPGRADE_LOCK_FILE" ] || return 0
    ST_UP_LOCK_PID="$(sed -n 's/^pid=//p' "$SILVERTASK_UPGRADE_LOCK_FILE" | head -1)"
    ST_UP_LOCK_TARGET="$(sed -n 's/^targetVersion=//p' "$SILVERTASK_UPGRADE_LOCK_FILE" | head -1)"
    ST_UP_LOCK_STARTED="$(sed -n 's/^startedAtUtc=//p' "$SILVERTASK_UPGRADE_LOCK_FILE" | head -1)"
    local probe_fd
    exec {probe_fd}>"$SILVERTASK_UPGRADE_LOCK_FILE"
    if flock -n "$probe_fd"; then
        flock -u "$probe_fd"
        exec {probe_fd}>&-
        return 0
    fi
    exec {probe_fd}>&-
    ST_UP_LOCK_ACTIVE=true
    return 1
}

# --- Upgrade state ---
#
# A small on-disk record of the most recent prepare attempt, read by --status. Distinct from the
# lock (which only exists while a process is actually running): the state file is deliberately
# left in place after completion/failure/a crash, so --status has something to report even once
# the lock itself is gone — that leftover record, combined with st_up_lock_probe reporting IDLE,
# is exactly what "STALE UPGRADE LOCK DETECTED" is built from (see update-debian.sh's cmd_status).
# Never auto-deleted by this file's own functions; only overwritten by the next real prepare
# attempt.

st_up_state_write() {
    local status="$1" current_version="$2" target_version="$3" current_step="$4" start_time="$5"
    cat > "$SILVERTASK_UPGRADE_STATE_FILE" <<EOF
{
  "currentVersion": "$(st_json_escape "$current_version")",
  "targetVersion": "$(st_json_escape "$target_version")",
  "startTimeUtc": "$(st_json_escape "$start_time")",
  "currentStep": "$(st_json_escape "$current_step")",
  "status": "$(st_json_escape "$status")",
  "lastUpdatedUtc": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
    chmod 640 "$SILVERTASK_UPGRADE_STATE_FILE" 2>/dev/null || true
}

# Prints each field as KEY=VALUE (one per line, safe for `while IFS== read`). Returns 1 (prints
# nothing) if no state has ever been recorded.
st_up_state_read() {
    [ -f "$SILVERTASK_UPGRADE_STATE_FILE" ] || return 1
    local field
    for field in currentVersion targetVersion startTimeUtc currentStep status lastUpdatedUtc; do
        printf '%s=%s\n' "$field" \
            "$(sed -n "s/.*\"$field\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\\1/p" "$SILVERTASK_UPGRADE_STATE_FILE" | head -1)"
    done
}

# --- Disk space (Phase 52 approximation — precise DB-backup sizing is explicitly Phase 53's job
# per the brief) ---
#
# Estimates required space as ~2x the current publish directory's size (room for a fresh worktree
# checkout of comparable size, plus headroom for the build that will eventually consume it) plus a
# fixed floor. Sets ST_UP_DISK_REQUIRED_MB/ST_UP_DISK_AVAILABLE_MB for reporting either way.
# Returns 1 only when the shortfall is clearly known — an undeterminable free-space reading (e.g.
# `df` unavailable) is reported as "unknown," not treated as a hard failure.
st_up_disk_space_check() {
    local floor_mb=512
    local publish_mb=0 avail_mb required_mb
    if [ -d "$SILVERTASK_PUBLISH_DIR" ]; then
        publish_mb="$(du -sm "$SILVERTASK_PUBLISH_DIR" 2>/dev/null | awk '{print $1}')"
    fi
    publish_mb="${publish_mb:-0}"
    required_mb=$(( publish_mb * 2 + floor_mb ))
    avail_mb="$(df -Pm "$SILVERTASK_INSTALL_DIR" 2>/dev/null | awk 'NR==2 {print $4}')"
    ST_UP_DISK_REQUIRED_MB="$required_mb"
    ST_UP_DISK_AVAILABLE_MB="${avail_mb:-unknown}"
    [ -n "${avail_mb:-}" ] || return 0
    [ "$avail_mb" -ge "$required_mb" ]
}

# --- Release metadata (releases/<version>.json — see README "Upgrade Engine") ---
#
# Reads releases/<version>.json's content directly out of the git object database at the target
# tag (`git show v<version>:releases/<version>.json`) rather than assuming a worktree already
# exists — deliberately, so a metadata peek is possible before (or entirely without) staging a
# full worktree. Requires the tag to already be fetchable locally; returns 1 (prints nothing) if
# it isn't, distinct from "fetched fine, file just doesn't exist at that tag" (prints nothing,
# returns 0) — st_up_metadata_validate treats both "doesn't exist" and "couldn't be read yet" the
# same way (documented safe defaults), but callers that care about the difference (dry-run,
# reporting "unverified" rather than silently defaulting) should check the exit status.
st_up_read_release_metadata() {
    local source_dir="$1" version="$2"
    git -C "$source_dir" show "v$version:$SILVERTASK_RELEASES_DIR/$version.json" 2>/dev/null
}

# Validates already-read metadata content (a JSON string, e.g. from st_up_read_release_metadata —
# never a file path, so this is pure string processing and independently testable). Populates
# ST_UP_META_* (requiresDatabaseMigration/requiresDataMigration/requiresRestart default to the
# documented safe values — migration assumed possibly-needed, restart assumed needed — whenever
# metadata_content is empty; "don't require every historical release to have metadata, but never
# silently assume unsafe compatibility" per the brief). Return codes:
#   0 = valid — either well-formed metadata, or none at all (safe defaults applied)
#   1 = malformed metadata (wrong "version" field / unsupported channel / invalid
#       minimumSupportedVersion) — must block the upgrade outright
#   2 = well-formed but blocks this upgrade on policy grounds (installed version doesn't satisfy
#       the release's declared minimumSupportedVersion)
st_up_metadata_validate() {
    local version="$1" metadata_content="$2" installed_version="$3"

    ST_UP_META_SOURCE="default (no releases/$version.json at tag v$version)"
    ST_UP_META_MIN_VERSION=""
    ST_UP_META_REQUIRES_DB_MIGRATION="true"
    ST_UP_META_REQUIRES_DATA_MIGRATION="false"
    ST_UP_META_REQUIRES_RESTART="true"

    [ -n "$metadata_content" ] || return 0
    ST_UP_META_SOURCE="releases/$version.json (tag v$version)"

    local meta_version meta_channel db_field data_field restart_field
    meta_version="$(printf '%s' "$metadata_content" | sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
    meta_channel="$(printf '%s' "$metadata_content" | sed -n 's/.*"channel"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
    ST_UP_META_MIN_VERSION="$(printf '%s' "$metadata_content" | sed -n 's/.*"minimumSupportedVersion"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
    db_field="$(printf '%s' "$metadata_content" | sed -n 's/.*"requiresDatabaseMigration"[[:space:]]*:[[:space:]]*\(true\|false\).*/\1/p' | head -1)"
    data_field="$(printf '%s' "$metadata_content" | sed -n 's/.*"requiresDataMigration"[[:space:]]*:[[:space:]]*\(true\|false\).*/\1/p' | head -1)"
    restart_field="$(printf '%s' "$metadata_content" | sed -n 's/.*"requiresRestart"[[:space:]]*:[[:space:]]*\(true\|false\).*/\1/p' | head -1)"
    [ -n "$db_field" ] && ST_UP_META_REQUIRES_DB_MIGRATION="$db_field"
    [ -n "$data_field" ] && ST_UP_META_REQUIRES_DATA_MIGRATION="$data_field"
    [ -n "$restart_field" ] && ST_UP_META_REQUIRES_RESTART="$restart_field"

    if [ "$meta_version" != "$version" ]; then
        st_error "Release metadata for v$version declares version \"$meta_version\", expected \"$version\"."
        return 1
    fi
    if [ "$meta_channel" != "stable" ]; then
        st_error "Release metadata for v$version has channel \"$meta_channel\" — Phase 52 supports \"stable\" only."
        return 1
    fi
    if [ -n "$ST_UP_META_MIN_VERSION" ] && ! st_up_semver_valid "$ST_UP_META_MIN_VERSION"; then
        st_error "Release metadata for v$version has an invalid minimumSupportedVersion \"$ST_UP_META_MIN_VERSION\"."
        return 1
    fi
    if [ -n "$ST_UP_META_MIN_VERSION" ] && [ -n "$installed_version" ] \
        && [ "$(st_up_semver_compare "$installed_version" "$ST_UP_META_MIN_VERSION")" -lt 0 ]; then
        st_error "Target $version requires at least version $ST_UP_META_MIN_VERSION; installed is $installed_version."
        return 2
    fi
    return 0
}

# --- Release preparation ---
#
# Stages a target release into an isolated git worktree — a second, independent checkout of just
# that tag, sharing objects with the existing repo — without ever touching $SILVERTASK_SOURCE_DIR
# (what the legacy full-update path fetches/builds from) or anything currently running. `version`
# must already be validated (st_up_semver_valid) by the caller before this is invoked; it is
# always passed as its own argv element to git, never interpolated into a shell string, so it
# cannot reach a shell/git command as anything other than a literal ref name.
st_up_prepare_worktree() {
    local source_dir="$1" version="$2" staging_root="$3"
    local target_dir="$staging_root/$version"
    local tag="v$version"

    git -C "$source_dir" fetch --tags >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1 || return 1
    git -C "$source_dir" rev-parse -q --verify "refs/tags/$tag" >/dev/null 2>&1 || return 1

    mkdir -p "$staging_root"
    if [ -d "$target_dir" ]; then
        # Leftover from an interrupted previous attempt — safe to discard and redo, never
        # something a Phase 52 prepare run needs to resume from (see the brief's own "do not
        # automatically continue a partially completed upgrade").
        git -C "$source_dir" worktree remove --force "$target_dir" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1 \
            || rm -rf "$target_dir"
    fi
    git -C "$source_dir" worktree add --detach "$target_dir" "$tag" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1 || return 1

    ST_UP_STAGED_DIR="$target_dir"
    return 0
}

st_up_cleanup_worktree() {
    local source_dir="$1" target_dir="$2"
    [ -n "$target_dir" ] && [ -d "$target_dir" ] || return 0
    git -C "$source_dir" worktree remove --force "$target_dir" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>&1 \
        || rm -rf "$target_dir"
}

# --- Upgrade log ---
#
# Separate from lib/common.sh's install log (SILVERTASK_LOG_FILE) — a running history of every
# upgrade-engine invocation (--check/--status/--latest/--target-version), not installer runs.
# Never called with secret values: every call site in update-debian.sh only ever passes version
# strings, step names, timestamps, and PIDs — the same discipline lib/common.sh's own logging
# functions already document and rely on.
st_up_log() {
    install -d -m 750 -o root -g root "$SILVERTASK_UPGRADE_LOG_DIR" 2>/dev/null || true
    local line
    line="[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"
    echo "$line"
    if [ -w "$SILVERTASK_UPGRADE_LOG_DIR" ] || [ -w "$SILVERTASK_UPGRADE_LOG_FILE" ] 2>/dev/null; then
        echo "$line" >> "$SILVERTASK_UPGRADE_LOG_FILE" 2>/dev/null || true
        chmod 640 "$SILVERTASK_UPGRADE_LOG_FILE" 2>/dev/null || true
    fi
}
