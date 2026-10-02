#!/usr/bin/env bash
#
# Start a dedicated DSH web instance for UniClaw live E2E.
#
# This script is intentionally explicit and non-destructive:
# - it never starts, restarts, or kills the owner's 3080 instance;
# - it refuses 3080 and refuses an already-occupied test port;
# - the caller owns the foreground process and its lifecycle.

set -euo pipefail

PORT="${DSH_TEST_PORT:-3081}"
DK_HARNESS_DIR="${DSH_HARNESS_DIR:-/Users/fran/Documents/Code/dk-harness}"
NODE_BIN="${DSH_NODE_BIN:-node}"
CLI="${DSH_CLI:-$DK_HARNESS_DIR/apps/cli/lib/bin.js}"

case "$PORT" in
  ''|*[!0-9]*) echo "ERROR: DSH_TEST_PORT must be a numeric port" >&2; exit 2 ;;
esac
if [ "$PORT" -eq 3080 ]; then
  echo "ERROR: refusing the owner's DSH port 3080; choose a dedicated test port (default: 3081)" >&2
  exit 2
fi
if [ ! -f "$CLI" ]; then
  echo "ERROR: DSH CLI not found: $CLI" >&2
  echo "Build dk-harness first or set DSH_CLI to its built CLI entrypoint." >&2
  exit 2
fi
if command -v lsof >/dev/null 2>&1 && lsof -nP -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null | tail -n +2 | grep -q .; then
  echo "ERROR: test port $PORT is already occupied; no process was stopped" >&2
  exit 2
fi

echo "Starting dedicated DSH test service on http://127.0.0.1:$PORT/"
echo "Run E2E with: UNICLAW_DSH_E2E_BASE=http://127.0.0.1:$PORT/"
exec "$NODE_BIN" "$CLI" web --no-open --port "$PORT"
