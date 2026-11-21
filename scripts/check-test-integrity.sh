#!/usr/bin/env bash
# Test-integrity check (Constitution Principle XVIII.2 / XIX — spike 059).
#
# Rule: after the oracle stage tagged `oracle/<N>-<feature>`, no test file may
# be MODIFIED or DELETED on the branch. New test files (the red team's
# tests/**/Attack/ lane, and late oracle additions re-tagged) are allowed.
#
# Usage: scripts/check-test-integrity.sh [oracle-tag]
#   Without an argument, uses the most recent oracle/* tag by commit date.
# A branch with no oracle/* tag FAILS: a gate that cannot be checked is not
# a gate (Principle XVII.2 — a step that cannot fail is not evidence).
set -euo pipefail

cd "$(dirname "$0")/../.."

TAG="${1:-$(git tag -l 'oracle/*' | while read -r t; do
  printf '%s\t%s\n' "$(git log -1 --format=%ct "$t")" "$t"
done | sort -n | tail -1 | cut -f2)}"

if [ -z "${TAG:-}" ]; then
  echo "TEST-INTEGRITY FAIL: no oracle/* tag on this branch — the oracle stage has not been committed (Principle XVIII)."
  exit 1
fi

echo "Checking test integrity against oracle tag: $TAG"

# 1) Any modification or deletion of a tracked test file after the oracle tag.
MODIFIED=$(git diff --diff-filter=DM --name-only "$TAG..HEAD" -- 'tests/' || true)
if [ -n "$MODIFIED" ]; then
  echo "TEST-INTEGRITY FAIL: test files modified or deleted after $TAG (Principle XVIII.2 — the implementer never edits tests):"
  echo "$MODIFIED"
  exit 1
fi

# 2) Explicit holdout guard (distinct failure message; same DM semantics).
HOLDOUT=$(git diff --diff-filter=DM --name-only "$TAG..HEAD" -- 'tests/*/Holdout/' 'tests/*/*/Holdout/' || true)
if [ -n "$HOLDOUT" ]; then
  echo "TEST-INTEGRITY FAIL: holdout suite modified or deleted after $TAG (Principle XVIII.2/XVIII.3):"
  echo "$HOLDOUT"
  exit 1
fi

echo "TEST-INTEGRITY PASS: no test modifications/deletions since $TAG (new files allowed: the Attack/ lane)."
exit 0
