#!/bin/bash
# Silver Task — version / git-tag compatibility check.
#
# Validates the repo-root VERSION file is well-formed semver, and — when HEAD is checked out
# exactly on a git tag (the state scripts/update-debian.sh --ref=<tag> leaves it in) — that the
# tag matches the VERSION file (tag "vX.Y.Z" == "v$(cat VERSION)"). Not an error when HEAD isn't
# on a tag (normal for day-to-day commits on the default branch); prints the current version and
# exits 0. Standalone — does not source lib/common.sh (no root/install-dir requirements; this is
# meant to be runnable by a developer against any checkout, not just an installed host).
#
# Usage:
#   ./scripts/check-version.sh [repo-dir]      # defaults to this script's own repo checkout

set -euo pipefail

REPO_DIR="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
VERSION_FILE="$REPO_DIR/VERSION"

if [ ! -f "$VERSION_FILE" ]; then
    echo "ERROR: $VERSION_FILE not found." >&2
    exit 1
fi

VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"

if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: VERSION file contents \"$VERSION\" are not valid semver (expected MAJOR.MINOR.PATCH)." >&2
    exit 1
fi

echo "Version: $VERSION"

if ! git -C "$REPO_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "Not a git checkout — skipping tag check."
    exit 0
fi

TAG="$(git -C "$REPO_DIR" describe --exact-match --tags 2>/dev/null || true)"
if [ -z "$TAG" ]; then
    echo "HEAD is not on an exact tag — skipping tag/version check (normal for day-to-day commits)."
    exit 0
fi

EXPECTED_TAG="v$VERSION"
if [ "$TAG" != "$EXPECTED_TAG" ]; then
    echo "ERROR: git tag \"$TAG\" does not match VERSION file (\"$VERSION\", expected tag \"$EXPECTED_TAG\")." >&2
    exit 1
fi

echo "Git tag \"$TAG\" matches VERSION file."
