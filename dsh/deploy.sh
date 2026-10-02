#!/usr/bin/env bash
#
# dsh/deploy.sh — deploy the four @uniclaw DSH profile plugins into the DSH web
# profile (~/.dsh/profiles/web) with mechanical drift detection.
#
# WHY THIS EXISTS: pnpm `file:` deps are SNAPSHOT COPIES, not symlinks, and pnpm
# reuses content-addressed snapshots — a plain `pnpm add file:...` may copy
# nothing new, so the profile can silently keep running STALE plugin code while
# logs look fine. The discipline (see docs/analysis/dsh-tool-scope-restricted-sessions.md
# §6 and docs/analysis/agt-002-b1-fix-instruction.md §4 M2 / §6) is:
#   remove + add  →  grep-verify a marker in the installed copy  →  restart instance.
# This script turns that discipline into tooling: pnpm remove+add per package,
# marker verification, and a full recursive diff (repo vs installed snapshot).
#
# HARD SIDE-EFFECT RULES: the only mutations are `pnpm remove/add` of the four
# @uniclaw packages under PROFILE_DIR (touching only node_modules there).
# Nothing else under ~/.dsh is ever modified; no instance is restarted here
# (restarting the owner's instance is a human decision).

set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROFILE_DIR="${DSH_PROFILE_DIR:-$HOME/.dsh/profiles/web}"
if [ -n "${DSH_PNPM_BIN:-}" ]; then
  PNPM_BIN="$DSH_PNPM_BIN"
elif [ -x /Users/fran/Documents/Code/dk-harness/node_modules/.bin/pnpm ]; then
  PNPM_BIN=/Users/fran/Documents/Code/dk-harness/node_modules/.bin/pnpm
elif command -v pnpm >/dev/null 2>&1; then
  PNPM_BIN=pnpm
else
  PNPM_BIN=""
fi

CHECK_ONLY=0
PORT_HINT=""

usage() {
  cat <<'EOF'
Usage: dsh/deploy.sh [--check-only] [--port N]

Deploys the @uniclaw DSH profile plugins (dsh-uniagent-restrict,
dsh-decision-channel, dsh-task-workbench, dsh-task-scope) into the DSH web
profile as file: snapshot deps,
verifies installed markers, and compares every repo file against the
installed snapshot to detect drift.

Options:
  --check-only   Run ONLY the drift check (no pnpm mutations). Exit 0 clean / 1 drift.
  --port N       After deploying, print (but do NOT run) the exact start command
                 for a dedicated test instance on the given port, plus the
                 preset-reload reminder. Do not pass the owner's 3080 port.
  -h, --help     Show this help.

Environment:
  DSH_PROFILE_DIR  Override profile dir (default ~/.dsh/profiles/web)
  DSH_PNPM_BIN     Override pnpm binary path (default dk-harness .bin/pnpm, else PATH)
EOF
}

say() { printf '\n== %s\n' "$*"; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

preflight() {
  say "Preflight"
  [ -n "$PNPM_BIN" ] || die "pnpm not found (set DSH_PNPM_BIN or put pnpm on PATH)"
  printf 'pnpm: %s\n' "$PNPM_BIN"
  printf 'profile dir: %s\n' "$PROFILE_DIR"
  [ -d "$PROFILE_DIR" ] || die "profile dir not found: $PROFILE_DIR"
  local pkg name
  for pkg in uniclaw-restrict uniclaw-decision-channel uniclaw-task-workbench uniclaw-task-scope; do
    [ -f "$REPO_DIR/dsh/$pkg/package.json" ] || die "missing $REPO_DIR/dsh/$pkg/package.json"
    [ -f "$REPO_DIR/dsh/$pkg/src/index.js" ] || die "missing $REPO_DIR/dsh/$pkg/src/index.js"
  done
  echo "repo package dirs: OK"
}

# deploy_one <name> <repo-abs-dir>: remove+add so pnpm cannot reuse a stale snapshot.
deploy_one() {
  local name="$1" dir="$2" out
  say "Deploy $name (remove + add, to defeat content-addressed snapshot reuse)"
  printf '$ %s remove %s --ignore-workspace (cwd %s)\n' "$PNPM_BIN" "$name" "$PROFILE_DIR"
  out="$(cd "$PROFILE_DIR" && "$PNPM_BIN" remove "$name" --ignore-workspace 2>&1)" \
    || {
      # First-time install: the package is not a profile dependency yet, so
      # remove has nothing to do. Only THIS error is tolerated; any other
      # remove failure stays fatal.
      if grep -q 'ERR_PNPM_CANNOT_REMOVE_MISSING_DEPS' <<<"$out"; then
        echo "not yet a dependency (first install); skip remove"
      else
        printf '%s\n' "$out"; die "pnpm remove $name failed"
      fi
    }
  printf '$ %s add %s@file:%s --ignore-workspace\n' "$PNPM_BIN" "$name" "$dir"
  out="$(cd "$PROFILE_DIR" && "$PNPM_BIN" add "$name@file:$dir" --ignore-workspace 2>&1)" \
    || { printf '%s\n' "$out"; die "pnpm add $name failed (profile dependency state may need manual attention)"; }
  echo "deployed."
}

# marker_present <file> <marker>: true if marker appears on a non-comment line.
marker_present() {
  local file="$1" marker="$2"
  # Strip comment-ish lines (/* opener, * continuation, // line) then grep
  # literally. NOTE: no `grep -q` here — -q exits at the first match and
  # SIGPIPEs the upstream grep -v under `set -o pipefail`, turning a present
  # marker into a false failure once the file crosses the pipe-buffer size
  # (seen with decision-channel src/index.js). Plain grep drains its input.
  grep -v -E '^[[:space:]]*(/\*|\*|//)' "$file" 2>/dev/null | grep -F -- "$marker" >/dev/null 2>&1
}

verify_markers() {
  say "Verify install markers"
  local f="$PROFILE_DIR/node_modules/@uniclaw/dsh-uniagent-restrict/src/index.js"
  if marker_present "$f" 'ctx.tools.restrict({ allow: [] })'; then
    echo "OK: restrict marker present in $f"
  else
    die "marker missing in $f
  expected (outside comments): ctx.tools.restrict({ allow: [] })"
  fi
  f="$PROFILE_DIR/node_modules/@uniclaw/dsh-decision-channel/src/index.js"
  if marker_present "$f" 'state.sessionToolDisposer = agentCtx.tools.register'; then
    echo "OK: decision-channel marker present in $f"
  else
    die "marker missing in $f
  expected (outside comments): state.sessionToolDisposer = agentCtx.tools.register"
  fi
  f="$PROFILE_DIR/node_modules/@uniclaw/dsh-task-workbench/src/index.js"
  if marker_present "$f" 'ctx.connection.fetch.register'; then
    echo "OK: task-workbench marker present in $f"
  else
    die "marker missing in $f
  expected (outside comments): ctx.connection.fetch.register"
  fi
  f="$PROFILE_DIR/node_modules/@uniclaw/dsh-task-scope/src/index.js"
  if marker_present "$f" 'ctx.tools.restrict({ allow })'; then
    echo "OK: task-scope marker present in $f"
  else
    die "marker missing in $f
  expected (outside comments): ctx.tools.restrict({ allow })"
  fi
}

# Drift check: every repo file must byte-match the installed snapshot.
# bash 3.2 portable: no associative arrays, no mapfile.
drift_check() {
  say "Drift check (repo vs installed snapshot)"
  local pkg name repo installed n=0 bad=0 rel
  for pkg in uniclaw-restrict uniclaw-decision-channel uniclaw-task-workbench uniclaw-task-scope; do
    case "$pkg" in
      uniclaw-restrict) name="@uniclaw/dsh-uniagent-restrict" ;;
      *) name="@uniclaw/dsh-${pkg#uniclaw-}" ;;
    esac
    repo="$REPO_DIR/dsh/$pkg"
    installed="$PROFILE_DIR/node_modules/$name"
    echo "-- $name"
    while IFS= read -r rel; do
      [ "$(basename "$rel")" = ".DS_Store" ] && continue
      n=$((n + 1))
      if [ ! -f "$installed/$rel" ]; then
        echo "MISSING in installed snapshot: $name/$rel"
        bad=$((bad + 1))
      elif ! diff -q "$repo/$rel" "$installed/$rel" >/dev/null 2>&1; then
        echo "DIFFERS: $name/$rel"
        bad=$((bad + 1))
      fi
    done < <(cd "$repo" && find . -type f ! -name '.DS_Store' | sed 's|^\./||' | sort)
  done
  if [ "$bad" -gt 0 ]; then
    echo "DRIFT CHECK: $bad file(s) missing/differing ($n files compared)"
    return 1
  fi
  echo "DRIFT CHECK: clean ($n files compared)"
}

port_hint() {
  say "Dedicated test-service hint (--port $PORT_HINT)"
  cat <<EOF
NEXT STEP (run manually — this script never starts or restarts anything):
  cd /Users/fran/Documents/Code/dk-harness && node apps/cli/lib/bin.js web --no-open --port $PORT_HINT

This is a dedicated test instance. It must use a port other than 3080.
The script never starts, restarts, or kills the owner's 3080 instance.

Reminder: profile patch preset rows (e.g. the @deepseek-ai/dsh-agent-preset
declaration lines in cordis.patch.yml) only load at boot — preset-composition
changes require the dedicated test instance to be started again.
EOF
}

main() {
  while [ $# -gt 0 ]; do
    case "$1" in
      --check-only) CHECK_ONLY=1 ;;
      --port) [ $# -ge 2 ] || die "--port requires a value"; PORT_HINT="$2"; shift ;;
      -h|--help) usage; exit 0 ;;
      *) usage >&2; die "unknown argument: $1" ;;
    esac
    shift
  done

  if [ -n "$PORT_HINT" ] && [ "$PORT_HINT" = "3080" ]; then
    die "--port 3080 is reserved for the owner's live service; choose a dedicated test port such as 3081"
  fi

  preflight

  if [ "$CHECK_ONLY" -eq 1 ]; then
    echo "mode: check-only (no mutations)"
    drift_check
    return
  fi

  echo "mode: deploy"
  deploy_one '@uniclaw/dsh-uniagent-restrict' "$REPO_DIR/dsh/uniclaw-restrict"
  deploy_one '@uniclaw/dsh-decision-channel' "$REPO_DIR/dsh/uniclaw-decision-channel"
  deploy_one '@uniclaw/dsh-task-workbench' "$REPO_DIR/dsh/uniclaw-task-workbench"
  deploy_one '@uniclaw/dsh-task-scope' "$REPO_DIR/dsh/uniclaw-task-scope"
  verify_markers
  drift_check
  [ -n "$PORT_HINT" ] && port_hint
  echo "Deploy complete."
}

main "$@"
