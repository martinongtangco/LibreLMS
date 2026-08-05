#!/usr/bin/env bash
# restart-app.sh — Kill running Host app, clean rebuild, and restart.
# Usage: ./scripts/restart-app.sh [--no-docker] [--background]
#
# Flags:
#   --no-docker   Skip docker compose (use when DB is already running)
#   --background  Run the app in the background (for non-interactive sessions)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/src/Host"
SOLUTION="$REPO_ROOT/LibreLms.slnx"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

info()  { echo -e "${GREEN}[restart-app]${NC} $*"; }
warn()  { echo -e "${YELLOW}[restart-app]${NC} $*"; }
fail()  { echo -e "${RED}[restart-app]${NC} $*" >&2; exit 1; }

# Parse flags
SKIP_DOCKER=false
RUN_BACKGROUND=false
for arg in "$@"; do
  case "$arg" in
    --no-docker)  SKIP_DOCKER=true ;;
    --background) RUN_BACKGROUND=true ;;
    *)            fail "Unknown flag: $arg" ;;
  esac
done

# 1. Kill any running Host app
info "Killing running Host app processes..."
PIDS=$(pgrep -f 'dotnet.*Host' 2>/dev/null || true)
if [ -n "$PIDS" ]; then
  echo "$PIDS" | xargs kill 2>/dev/null || true
  sleep 1
  # Force-kill anything still alive
  PIDS=$(pgrep -f 'dotnet.*Host' 2>/dev/null || true)
  if [ -n "$PIDS" ]; then
    echo "$PIDS" | xargs kill -9 2>/dev/null || true
  fi
  info "Killed $PIDS"
else
  info "No running Host app found"
fi

# 2. Optional: ensure Docker services are up
if [ "$SKIP_DOCKER" = false ]; then
  if command -v docker &>/dev/null; then
    info "Starting Docker services (MSSQL, Valkey)..."
    cd "$REPO_ROOT"
    docker compose up -d 2>&1 | tail -3
    info "Waiting for services to be healthy..."
    sleep 5
  else
    warn "Docker not available — skipping service startup"
  fi
fi

# 3. Clean build artifacts for Host
info "Cleaning Host build artifacts..."
rm -rf "$PROJECT_DIR/obj" "$PROJECT_DIR/bin"

# 4. Restore and build
info "Restoring and rebuilding..."
cd "$REPO_ROOT"
# Restore using solution if it exists, otherwise restore the project directly
if [ -f "$SOLUTION" ]; then
  dotnet restore "$SOLUTION" --verbosity quiet 2>&1 || true
else
  dotnet restore "$PROJECT_DIR" --verbosity quiet 2>&1 || true
fi
dotnet build "$PROJECT_DIR" --no-restore 2>&1 | tail -5

BUILD_EXIT=${PIPESTATUS[0]}
if [ "$BUILD_EXIT" -ne 0 ]; then
  fail "Build failed with exit code $BUILD_EXIT"
fi

info "Build succeeded"

# 5. Run the app
info "Starting Host app..."
cd "$PROJECT_DIR"
if [ "$RUN_BACKGROUND" = true ]; then
  dotnet run --urls "https://localhost:7095;http://localhost:5000" &
  APP_PID=$!
  info "App started in background (PID: $APP_PID)"
  info "Waiting for startup..."
  sleep 8
  if kill -0 "$APP_PID" 2>/dev/null; then
    info "App is running on https://localhost:7095 and http://localhost:5000"
  else
    fail "App crashed during startup. Check logs above."
  fi
else
  exec dotnet run --urls "https://localhost:7095;http://localhost:5000"
fi
