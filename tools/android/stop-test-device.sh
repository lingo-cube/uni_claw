#!/usr/bin/env bash
set -euo pipefail

run_dir=${1:-}
if [[ -z "$run_dir" || ! -f "$run_dir/.state" ]]; then
  echo "usage: $0 /tmp/uniclaw-android-<run-id>" >&2
  exit 2
fi
source "$run_dir/.state"
adb_bin=${ANDROID_SDK_ROOT:+${ANDROID_SDK_ROOT}/platform-tools/adb}
adb_bin=${adb_bin:-${ANDROID_HOME:+${ANDROID_HOME}/platform-tools/adb}}
adb_bin=${adb_bin:-adb}
"$adb_bin" -s "$SERIAL" emu kill >/dev/null 2>&1 || true
if kill -0 "$PID" >/dev/null 2>&1; then kill "$PID" >/dev/null 2>&1 || true; fi
for _ in {1..20}; do
  kill -0 "$PID" >/dev/null 2>&1 || break
  sleep 1
done
rm -rf -- "$run_dir"
echo "stopped $SERIAL"
