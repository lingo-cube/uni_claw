#!/usr/bin/env bash
set -euo pipefail

# Manual-only sync. Nothing invokes this script automatically.
# Usage examples:
#   bash scripts/sync-universal-agent-guideline.sh --target codex
#   bash scripts/sync-universal-agent-guideline.sh --target codex --apply
#   bash scripts/sync-universal-agent-guideline.sh --file "$HOME/path/to/agent-global-instructions.md" --apply

ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
SOURCE="$ROOT/.ai/universal-agent-guideline.md"
BEGIN='<!-- BEGIN UNIVERSAL AGENT GUIDELINE BASELINE -->'
END='<!-- END UNIVERSAL AGENT GUIDELINE BASELINE -->'
TARGET=''
APPLY=0

die() {
  printf 'error: %s\n' "$1" >&2
  exit 2
}

usage() {
  cat <<'EOF'
Usage:
  sync-universal-agent-guideline.sh --target codex [--apply]
  sync-universal-agent-guideline.sh --file ABSOLUTE_PATH [--apply]

Default is preview-only. A write requires the explicit --apply flag.
Known targets:
  codex   $CODEX_HOME/AGENTS.md, or ~/.codex/AGENTS.md
Use --file for other coding agents whose global instruction path is user-defined;
the target must accept Markdown, and any platform-specific frontmatter remains outside the managed block.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --target)
      [ "$#" -ge 2 ] || die '--target requires codex'
      case "$2" in
        codex) TARGET="${CODEX_HOME:-$HOME/.codex}/AGENTS.md" ;;
        *) die "unknown target: $2 (use --file for other agents)" ;;
      esac
      shift 2
      ;;
    --file)
      [ "$#" -ge 2 ] || die '--file requires an absolute path'
      case "$2" in
        /*) TARGET="$2" ;;
        *) die '--file must be an absolute path' ;;
      esac
      shift 2
      ;;
    --apply)
      APPLY=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *) die "unknown argument: $1" ;;
  esac
done

[ -n "$TARGET" ] || { usage >&2; exit 2; }
[ -f "$SOURCE" ] || die "source not found: $SOURCE"
[ -s "$SOURCE" ] || die "source is empty: $SOURCE"
[ "$TARGET" != "$SOURCE" ] || die 'target must not be the versioned source file'
if grep -Fq "$BEGIN" "$SOURCE" || grep -Fq "$END" "$SOURCE"; then
  die 'source must not contain sync markers'
fi
[ -e "$TARGET" ] && [ ! -f "$TARGET" ] && die "target is not a regular file: $TARGET"

if [ -f "$TARGET" ]; then
  BEGIN_COUNT=$(grep -Fxc "$BEGIN" "$TARGET" || true)
  END_COUNT=$(grep -Fxc "$END" "$TARGET" || true)
  [ "$BEGIN_COUNT" -le 1 ] && [ "$END_COUNT" -le 1 ] \
    || die 'target contains duplicate sync markers'
  [ "$BEGIN_COUNT" -eq "$END_COUNT" ] \
    || die 'target contains an incomplete sync block; repair it manually before syncing'
fi

TARGET_DIR=$(dirname -- "$TARGET")
if [ "$APPLY" -eq 1 ]; then
  mkdir -p "$TARGET_DIR"
fi

strip_managed_block() {
  if [ -f "$TARGET" ]; then
    awk -v begin="$BEGIN" -v end="$END" '
      $0 == begin { skipping=1; next }
      $0 == end { skipping=0; next }
      !skipping { print }
    ' "$TARGET"
  fi
}

build_candidate() {
  strip_managed_block
  printf '\n%s\n' "$BEGIN"
  cat "$SOURCE"
  printf '%s\n' "$END"
}

TMP=$(mktemp "${TMPDIR:-/tmp}/agent-guideline-sync.XXXXXX")
trap 'rm -f "$TMP"' EXIT
build_candidate > "$TMP"

if [ "$APPLY" -eq 0 ]; then
  printf 'preview only: %s\n' "$TARGET"
  if [ -f "$TARGET" ]; then
    diff -u "$TARGET" "$TMP" || true
  else
    cat "$TMP"
  fi
  printf 'no files changed; rerun with --apply to write and create a rollback backup.\n'
  exit 0
fi

TIMESTAMP=$(date '+%Y%m%d%H%M%S')
if [ -f "$TARGET" ]; then
  BACKUP="$TARGET.bak.$TIMESTAMP"
  BACKUP_INDEX=0
  while [ -e "$BACKUP" ]; do
    BACKUP_INDEX=$((BACKUP_INDEX + 1))
    BACKUP="$TARGET.bak.$TIMESTAMP.$BACKUP_INDEX"
  done
  cp -p "$TARGET" "$BACKUP"
  printf 'backup: %s\n' "$BACKUP"
fi

if [ -f "$TARGET" ]; then
  MODE=$(stat -f '%Lp' "$TARGET" 2>/dev/null || true)
  if [ -n "$MODE" ]; then
    chmod "$MODE" "$TMP"
  fi
fi
mv "$TMP" "$TARGET"
trap - EXIT
printf 'updated: %s\n' "$TARGET"
