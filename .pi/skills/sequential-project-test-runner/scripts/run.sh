#!/usr/bin/env bash
# Runs each tests/* project sequentially against the shared MSSQL database.
# See ../SKILL.md for why this exists instead of `dotnet test LibreLms.slnx`.
set -uo pipefail

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

echo "==> dotnet restore LibreLms.slnx"
dotnet restore LibreLms.slnx || { echo "restore failed"; exit 1; }

total_pass=0
total_fail=0
total_skip=0
overall_status=0
failed_projects=()

for proj_dir in tests/*/; do
  name=$(basename "$proj_dir")
  csproj=$(find "$proj_dir" -maxdepth 1 -name "*.csproj" | head -1)
  [ -z "$csproj" ] && continue

  echo
  echo "==> $name"
  output=$(dotnet test "$csproj" --no-restore 2>&1)
  status=$?
  echo "$output" | tail -25

  summary=$(echo "$output" | grep -E "Passed!|Failed!|Total:" | tail -1)
  p=$(echo "$summary" | grep -oE "Passed: *[0-9]+" | grep -oE "[0-9]+" | head -1)
  f=$(echo "$summary" | grep -oE "Failed: *[0-9]+" | grep -oE "[0-9]+" | head -1)
  s=$(echo "$summary" | grep -oE "Skipped: *[0-9]+" | grep -oE "[0-9]+" | head -1)
  p=${p:-0}; f=${f:-0}; s=${s:-0}

  total_pass=$((total_pass + p))
  total_fail=$((total_fail + f))
  total_skip=$((total_skip + s))

  if [ "$status" -ne 0 ]; then
    overall_status=1
    failed_projects+=("$name")
  fi

  echo "-- $name: passed=$p failed=$f skipped=$s"
done

echo
echo "===== TOTAL: passed=$total_pass failed=$total_fail skipped=$total_skip ====="
if [ "$overall_status" -ne 0 ]; then
  echo "Failed projects: ${failed_projects[*]}"
fi
exit $overall_status
