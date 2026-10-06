#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
supervisor_pid=""
adb_bin=""
serial=""
ready=0
cleanup_failed_start() {
  local status=$?
  if [[ "$ready" -eq 0 && -n "$supervisor_pid" && -n "$adb_bin" && -n "$serial" ]]; then
    set +e
    "$adb_bin" -s "$serial" emu kill >/dev/null 2>&1 || true
    kill "$supervisor_pid" >/dev/null 2>&1 || true
    set -e
  fi
  exit "$status"
}
trap cleanup_failed_start EXIT

sdk_root=${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}
avd_home=${ANDROID_AVD_HOME:-${HOME}/.android/avd}
avd_name=${UNICLAW_ANDROID_AVD:-}
target_wm_size=${UNICLAW_ANDROID_WM_SIZE:-1080x1920}
if [[ ! "$target_wm_size" =~ ^[1-9][0-9]*x[1-9][0-9]*$ ]]; then
  echo "ENVIRONMENT_UNAVAILABLE: invalid UNICLAW_ANDROID_WM_SIZE: $target_wm_size (expected WIDTHxHEIGHT)" >&2
  exit 2
fi
if [[ -z "$avd_name" && -f "$avd_home/p26_pixel.ini" ]]; then
  avd_name=p26_pixel
fi
if [[ -z "$avd_name" ]]; then
  first_ini=$(find "$avd_home" -maxdepth 1 -type f -name '*.ini' -print -quit 2>/dev/null || true)
  [[ -n "$first_ini" ]] && avd_name=$(basename "$first_ini" .ini)
fi
if [[ -z "$avd_name" || ! -f "$avd_home/${avd_name}.ini" ]]; then
  echo "ENVIRONMENT_UNAVAILABLE: no AVD found under $avd_home (set UNICLAW_ANDROID_AVD)" >&2
  exit 2
fi
base_dir="$avd_home/${avd_name}.avd"
if [[ ! -d "$base_dir" ]]; then
  echo "ENVIRONMENT_UNAVAILABLE: AVD data directory missing: $base_dir" >&2
  exit 2
fi
run_id="$(date +%Y%m%d-%H%M%S)-$$"
run_dir="${TMPDIR:-/tmp}/uniclaw-android-${run_id}"
clone_dir="$run_dir/${avd_name}.avd"
mkdir -p "$run_dir"
if ! cp -cR "$base_dir" "$clone_dir" 2>/dev/null; then cp -R "$base_dir" "$clone_dir"; fi
port=5556
while adb -s "emulator-$port" get-state >/dev/null 2>&1; do
  port=$((port + 2))
  if [[ "$port" -gt 5580 ]]; then
    echo "ENVIRONMENT_UNAVAILABLE: no free emulator port in 5556..5580" >&2
    exit 3
  fi
done
serial="emulator-$port"
emulator_bin="${sdk_root:+$sdk_root/emulator/emulator}"
adb_bin="${sdk_root:+$sdk_root/platform-tools/adb}"
python_bin=$(command -v python3 || true)
[[ -n "$emulator_bin" && -x "$emulator_bin" ]] || emulator_bin=$(command -v emulator || true)
[[ -n "$adb_bin" && -x "$adb_bin" ]] || adb_bin=$(command -v adb || true)
[[ -n "$emulator_bin" && -x "$emulator_bin" ]] || { echo "ENVIRONMENT_UNAVAILABLE: emulator binary not found" >&2; exit 2; }
[[ -n "$adb_bin" && -x "$adb_bin" ]] || { echo "ENVIRONMENT_UNAVAILABLE: adb binary not found" >&2; exit 2; }
[[ -n "$python_bin" && -x "$python_bin" ]] || { echo "ENVIRONMENT_UNAVAILABLE: python3 not found for emulator supervisor" >&2; exit 2; }
supervisor="$root/tools/android/emulator-supervisor.py"
[[ -f "$supervisor" ]] || { echo "ENVIRONMENT_UNAVAILABLE: emulator supervisor missing: $supervisor" >&2; exit 2; }
nohup "$python_bin" "$supervisor" "$emulator_bin" -avd "$avd_name" -datadir "$clone_dir" -port "$port" -read-only -no-snapshot -no-snapshot-save -no-boot-anim >"$run_dir/emulator.log" 2>&1 < /dev/null &
pid=$!
supervisor_pid="$pid"
cat >"$run_dir/.state" <<EOF
PID=$pid
SERIAL=$serial
RUN_DIR=$run_dir
CLONE_DIR=$clone_dir
SUPERVISOR=emulator-supervisor
EOF
deadline=$((SECONDS + 180))
while ! "$adb_bin" -s "$serial" get-state >/dev/null 2>&1; do
  (( SECONDS >= deadline )) && { echo "ENVIRONMENT_UNAVAILABLE: adb wait-for-device timeout ($serial)" >&2; exit 3; }
  sleep 2
done
while [[ "$("$adb_bin" -s "$serial" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" != "1" ]]; do
  (( SECONDS >= deadline )) && { echo "ENVIRONMENT_UNAVAILABLE: boot timeout ($serial)" >&2; exit 3; }
  sleep 2
done
api="$($adb_bin -s "$serial" shell getprop ro.build.version.sdk | tr -d '\r')"
[[ "$api" =~ ^[0-9]+$ && "$api" -ge 35 ]] || { echo "ENVIRONMENT_UNAVAILABLE: API $api < 35" >&2; exit 4; }
size="$($adb_bin -s "$serial" shell wm size | tr -d '\r')"
[[ -n "$size" ]] || { echo "ENVIRONMENT_UNAVAILABLE: wm size unavailable" >&2; exit 4; }
"$adb_bin" -s "$serial" shell wm size "$target_wm_size" >/dev/null || {
  echo "ENVIRONMENT_UNAVAILABLE: unable to set wm size to $target_wm_size" >&2
  exit 4
}
size="$($adb_bin -s "$serial" shell wm size | tr -d '\r')"
if ! grep -Eq "(^|[[:space:]])Override size: ${target_wm_size}([[:space:]]|$)" <<<"$size"; then
  echo "ENVIRONMENT_UNAVAILABLE: wm size override did not apply (requested $target_wm_size; observed: $size)" >&2
  exit 4
fi
"$adb_bin" -s "$serial" exec-out screencap -p >/dev/null || { echo "ENVIRONMENT_UNAVAILABLE: screenshot unavailable" >&2; exit 4; }
"$adb_bin" -s "$serial" shell uiautomator dump /sdcard/window.xml >/dev/null || { echo "ENVIRONMENT_UNAVAILABLE: uiautomator unavailable" >&2; exit 4; }
"$adb_bin" -s "$serial" shell settings get global wifi_on >/dev/null || { echo "ENVIRONMENT_UNAVAILABLE: settings fixture unavailable" >&2; exit 4; }
ready=1
echo "UNICLAW_ANDROID_DEVICE=$serial"
echo "UNICLAW_ANDROID_RUN_DIR=$run_dir"
echo "android_api=$api"
echo "wm_size=$size"
echo "wm_size_target=$target_wm_size"
echo "To stop: tools/android/stop-test-device.sh $run_dir"
