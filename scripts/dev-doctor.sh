#!/usr/bin/env bash

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SOLUTION_PATH="${REPO_ROOT}/src/UniClaw.Core.sln"

RUN_BUILD=0
RUN_CODEX=0
RUN_EMULATOR=0
RUN_TEST=0

print_usage() {
  cat <<'EOF'
Usage: scripts/dev-doctor.sh [--build] [--test] [--codex] [--emulator]

Default checks are local and do not start the Android Emulator or perform a
network reachability probe.

Options:
  --build      Run dotnet build for src/UniClaw.Core.sln.
  --test       Run dotnet test for src/UniClaw.Core.sln.
  --codex      Run codex doctor --summary. This may report network reachability.
  --emulator   Run scripts/android-emulator.sh doctor. This requires a running
               emulator and never starts one.
EOF
}

note() {
  printf '\n[%s]\n' "$1"
}

ok() {
  printf 'ok: %s\n' "$1"
}

warn() {
  printf 'warn: %s\n' "$1" >&2
}

failures=0

run_check() {
  local label="$1"
  shift

  if "$@"; then
    ok "${label}"
  else
    warn "${label}"
    failures=$((failures + 1))
  fi
}

check_git_worktree() {
  git rev-parse --show-toplevel >/dev/null
}

check_command() {
  command -v "$1" >/dev/null
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) RUN_BUILD=1 ;;
    --test) RUN_TEST=1 ;;
    --codex) RUN_CODEX=1 ;;
    --emulator) RUN_EMULATOR=1 ;;
    -h|--help) print_usage; exit 0 ;;
    *) print_usage >&2; exit 2 ;;
  esac
  shift
done

cd "${REPO_ROOT}" || exit 1

note "Repository"
run_check "inside git worktree" check_git_worktree
run_check "solution exists" test -f "${SOLUTION_PATH}"
run_check "ripgrep available" check_command rg

note "Dotnet"
run_check "dotnet available" check_command dotnet
if command -v dotnet >/dev/null 2>&1; then
  dotnet --version
fi

note "Codex MCP"
if command -v codex >/dev/null 2>&1; then
  codex mcp list || failures=$((failures + 1))
else
  warn "codex CLI unavailable"
  failures=$((failures + 1))
fi

if [[ "${RUN_BUILD}" == "1" ]]; then
  note "Build"
  run_check "dotnet build" dotnet build "${SOLUTION_PATH}" -nr:false -m:1 -v:minimal -p:NuGetAudit=false
fi

if [[ "${RUN_TEST}" == "1" ]]; then
  note "Test"
  run_check "dotnet test" dotnet test "${SOLUTION_PATH}" -nr:false -m:1 -v:minimal -p:NuGetAudit=false
fi

if [[ "${RUN_EMULATOR}" == "1" ]]; then
  note "Android Emulator"
  run_check "emulator doctor" "${SCRIPT_DIR}/android-emulator.sh" doctor
fi

if [[ "${RUN_CODEX}" == "1" ]]; then
  note "Codex Doctor"
  codex doctor --summary || failures=$((failures + 1))
fi

if [[ "${failures}" -gt 0 ]]; then
  warn "dev doctor completed with ${failures} warning(s)"
  exit 1
fi

ok "dev doctor completed"
