#!/usr/bin/env bash
#
# dsh/deploy.sh — deploy the two @uniclaw DSH profile plugins into the DSH web
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
# HARD SIDE-EFFECT RULES: the only mutations are `pnpm remove/add` of the two
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
dsh-decision-channel) into the DSH web profile as file: snapshot deps,
verifies installed markers, and compares every repo file against the
installed snapshot to detect drift.

Options:
  --check-only   Run ONLY the drift check (no pnpm mutations). Exit 0 clean / 1 drift.
  --port N       After deploying, print (but do NOT run) the exact restart command
                 for the given port, plus the preset-reload reminder.
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
  for pkg in uniclaw-restrict uniclaw-decision-channel; do
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
    || { printf '%s\n' "$out"; die "pnpm remove $name failed"; }
  printf '$ %s add %s@file:%s --ignore-workspace\n' "$PNPM_BIN" "$name" "$dir"
  out="$(cd "$PROFILE_DIR" && "$PNPM_BIN" add "$name@file:$dir" --ignore-workspace 2>&1)" \
    || { printf '%s\n' "$out"; die "pnpm add $name failed (profile dependency state may need manual attention)"; }
  echo "deployed."
}

# marker_present <file> <marker>: true if marker appears on a non-comment line.
marker_present() {
  local file="$1" marker="$2"
  # Strip comment-ish lines (/* opener, * continuation, // line) then grep literally.
  grep -v -E '^[[:space:]]*(/\*|\*|//)' "$file" 2>/dev/null | grep -Fq -- "$marker"
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
  if marker_present "$f" 'sessionToolDisposer = agentCtx.tools.register'; then
    echo "OK: decision-channel marker present in $f"
  else
    die "marker missing in $f
  expected (outside comments): sessionToolDisposer = agentCtx.tools.register"
  fi
}

# Drift check: every repo file must byte-match the installed snapshot.
# bash 3.2 portable: no associative arrays, no mapfile.
drift_check() {
  say "Drift check (repo vs installed snapshot)"
  local pkg name repo installed n=0 bad=0 rel
  for pkg in uniclaw-restrict uniclaw-decision-channel; do
    name="@uniclaw/dsh-$([ "$pkg" = uniclaw-restrict ] && echo uniagent-restrict || echo decision-channel)"
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
  say "Restart hint (--port $PORT_HINT)"
  cat <<EOF
NEXT STEP (run manually — this script never restarts anything):
  cd /Users/fran/Documents/Code/dk-harness && node apps/cli/lib/bin.js web --no-open --port $PORT_HINT

Reminder: profile patch preset rows (e.g. the @deepseek-ai/dsh-agent-preset
declaration lines in cordis.patch.yml) only load at boot — preset-composition
changes require an instance restart to take effect.
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

  preflight

  if [ "$CHECK_ONLY" -eq 1 ]; then
    echo "mode: check-only (no mutations)"
    drift_check
    return
  fi

  echo "mode: deploy"
  deploy_one '@uniclaw/dsh-uniagent-restrict' "$REPO_DIR/dsh/uniclaw-restrict"
  deploy_one '@uniclaw/dsh-decision-channel' "$REPO_DIR/dsh/uniclaw-decision-channel"
  verify_markers
  drift_check
  [ -n "$PORT_HINT" ] && port_hint
  echo "Deploy complete."
}

main "$@"
