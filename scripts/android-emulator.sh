#!/usr/bin/env bash

set -u

DEFAULT_AVD_NAME="uniclaw-lite-api35"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

if [[ -n "${UNICLAW_ADB_SERVER_PORT:-}" && -z "${ANDROID_ADB_SERVER_PORT:-}" ]]; then
  export ANDROID_ADB_SERVER_PORT="${UNICLAW_ADB_SERVER_PORT}"
fi

if [[ -n "${ANDROID_SDK_ROOT:-}" ]]; then
  SDK_ROOT="${ANDROID_SDK_ROOT}"
elif [[ -n "${ANDROID_HOME:-}" ]]; then
  SDK_ROOT="${ANDROID_HOME}"
elif [[ "$(uname -s)" == "Darwin" ]]; then
  SDK_ROOT="${HOME}/Library/Android/sdk"
else
  SDK_ROOT="${HOME}/Android/Sdk"
fi

AVD_NAME="${UNICLAW_AVD_NAME:-${DEFAULT_AVD_NAME}}"
if [[ -n "${ADB_BIN:-}" ]]; then
  :
elif [[ -x "${SDK_ROOT}/platform-tools/adb" ]]; then
  ADB_BIN="${SDK_ROOT}/platform-tools/adb"
else
  ADB_BIN="$(command -v adb 2>/dev/null || true)"
fi
if [[ -n "${EMULATOR_BIN:-}" ]]; then
  :
elif [[ -x "${SDK_ROOT}/emulator/emulator" ]]; then
  EMULATOR_BIN="${SDK_ROOT}/emulator/emulator"
else
  EMULATOR_BIN="$(command -v emulator 2>/dev/null || true)"
fi
BOOT_TIMEOUT="${UNICLAW_EMULATOR_BOOT_TIMEOUT:-180}"
POLL_INTERVAL="${UNICLAW_EMULATOR_POLL_INTERVAL:-2}"

print_usage() {
  cat <<'EOF'
Usage: scripts/android-emulator.sh <doctor|start|stop>

Environment:
  ANDROID_SDK_ROOT              Android SDK root (default: platform default)
  UNICLAW_AVD_NAME              AVD name (default: uniclaw-lite-api35)
  UNICLAW_EMULATOR_SERIAL       Explicit adb serial, e.g. emulator-5554
  UNICLAW_ADB_SERVER_PORT       Optional adb server port override, e.g. 5038
  UNICLAW_EMULATOR_HEADLESS=1   Start without a GUI window
  UNICLAW_EMULATOR_SNAPSHOT     Optional snapshot name
  UNICLAW_EMULATOR_GPU          Optional emulator GPU mode
  UNICLAW_EMULATOR_BOOT_TIMEOUT Boot timeout in seconds (default: 180)
  UNICLAW_APK_PATH              Optional APK to validate
  UNICLAW_PACKAGE               Optional installed package to validate
EOF
}

fail() {
  echo "android-emulator: $*" >&2
  exit 1
}

require_file() {
  local label="$1"
  local path="$2"
  [[ -x "${path}" ]] || fail "${label} not found or not executable: ${path}. Set ANDROID_SDK_ROOT or the corresponding *_BIN override."
}

require_tools() {
  require_file "adb" "${ADB_BIN}"
  require_file "emulator" "${EMULATOR_BIN}"
}

list_avds() {
  "${EMULATOR_BIN}" -list-avds 2>/dev/null || true
}

has_avd() {
  list_avds | awk -v expected="${AVD_NAME}" '$0 == expected { found = 1 } END { exit(found ? 0 : 1) }'
}

running_serial() {
  if [[ -n "${UNICLAW_EMULATOR_SERIAL:-}" ]]; then
    printf '%s\n' "${UNICLAW_EMULATOR_SERIAL}"
    return 0
  fi

  "${ADB_BIN}" devices 2>/dev/null \
    | awk '$1 ~ /^emulator-[0-9]+$/ && $2 == "device" { print $1; exit }'
}

adb_cmd() {
  local serial="$1"
  shift
  "${ADB_BIN}" -s "${serial}" "$@"
}

wait_for_device() {
  local deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
  local serial=""

  while [[ $(date +%s) -lt ${deadline} ]]; do
    serial="$(running_serial)"
    if [[ -n "${serial}" ]]; then
      printf '%s\n' "${serial}"
      return 0
    fi
    sleep "${POLL_INTERVAL}"
  done

  echo "Connected devices:" >&2
  "${ADB_BIN}" devices >&2 || true
  return 1
}

wait_for_boot() {
  local serial="$1"
  local deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
  local boot_complete=""

  while [[ $(date +%s) -lt ${deadline} ]]; do
    boot_complete="$(adb_cmd "${serial}" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r\n[:space:]')"
    if [[ "${boot_complete}" == "1" ]]; then
      return 0
    fi
    sleep "${POLL_INTERVAL}"
  done

  echo "Boot did not complete for ${serial}." >&2
  adb_cmd "${serial}" shell getprop sys.boot_completed >&2 || true
  return 1
}

probe_screen() {
  local serial="$1"
  local screen_file
  local bytes
  screen_file="$(mktemp -t uniclaw-emulator-screen.XXXXXX)" || return 1

  if ! adb_cmd "${serial}" exec-out screencap -p >"${screen_file}" 2>/tmp/uniclaw-emulator-screencap.err; then
    cat /tmp/uniclaw-emulator-screencap.err >&2 || true
    rm -f "${screen_file}"
    return 1
  fi

  bytes="$(wc -c <"${screen_file}" | tr -d '[:space:]')"
  local magic
  magic="$(head -c 8 "${screen_file}" | od -An -tx1 | tr -d '[:space:]')"
  rm -f "${screen_file}"
  [[ "${bytes}" -gt 100 ]] || { echo "screencap returned ${bytes} bytes" >&2; return 1; }
  [[ "${magic}" == "89504e470d0a1a0a" ]] || { echo "screencap did not return a PNG" >&2; return 1; }
}

probe_uiautomator() {
  local serial="$1"
  local xml_file
  local bytes
  xml_file="$(mktemp -t uniclaw-emulator-ui.XXXXXX)" || return 1

  if ! adb_cmd "${serial}" shell uiautomator dump /sdcard/uniclaw-window.xml >/tmp/uniclaw-emulator-uiautomator.out 2>&1; then
    cat /tmp/uniclaw-emulator-uiautomator.out >&2 || true
    rm -f "${xml_file}"
    return 1
  fi
  if ! adb_cmd "${serial}" exec-out cat /sdcard/uniclaw-window.xml >"${xml_file}" 2>/tmp/uniclaw-emulator-uiautomator-cat.err; then
    cat /tmp/uniclaw-emulator-uiautomator-cat.err >&2 || true
    rm -f "${xml_file}"
    return 1
  fi

  bytes="$(wc -c <"${xml_file}" | tr -d '[:space:]')"
  if [[ "${bytes}" -le 20 ]] || ! head -c 200 "${xml_file}" | grep -q '<hierarchy'; then
    echo "uiautomator dump did not return hierarchy XML (${bytes} bytes)" >&2
    rm -f "${xml_file}"
    return 1
  fi
  if command -v python3 >/dev/null 2>&1; then
    if ! python3 -c 'import sys, xml.etree.ElementTree as ET; ET.parse(sys.argv[1])' "${xml_file}" 2>/tmp/uniclaw-emulator-xml.err; then
      cat /tmp/uniclaw-emulator-xml.err >&2 || true
      rm -f "${xml_file}"
      return 1
    fi
  fi
  rm -f "${xml_file}"
}

probe_optional_app() {
  local serial="$1"
  local apk_path="${UNICLAW_APK_PATH:-}"
  local package_name="${UNICLAW_PACKAGE:-}"

  [[ -z "${apk_path}" && -z "${package_name}" ]] && return 0
  [[ -n "${apk_path}" && -n "${package_name}" ]] || fail "UNICLAW_APK_PATH and UNICLAW_PACKAGE must be provided together."
  [[ -f "${apk_path}" ]] || fail "APK not found: ${apk_path}"
  adb_cmd "${serial}" shell pm path "${package_name}" >/dev/null 2>&1 \
    || fail "Package is not installed on ${serial}: ${package_name}. Install it explicitly before running doctor."
}

doctor() {
  require_tools
  has_avd || fail "AVD '${AVD_NAME}' not found. Available AVDs: $(list_avds | tr '\n' ' ')"

  local serial
  serial="$(running_serial)"
  [[ -n "${serial}" ]] || fail "No running Emulator found. Start it with: $0 start"
  wait_for_boot "${serial}" || fail "Emulator ${serial} is not ready."
  probe_screen "${serial}" || fail "Screenshot capability probe failed for ${serial}."
  probe_uiautomator "${serial}" || fail "UIAutomator capability probe failed for ${serial}."
  probe_optional_app "${serial}"

  echo "Android Emulator ready"
  echo "  SDK root: ${SDK_ROOT}"
  echo "  ADB: ${ADB_BIN}"
  echo "  Emulator: ${EMULATOR_BIN}"
  echo "  AVD: ${AVD_NAME}"
  echo "  Serial: ${serial}"
  echo "  Screen: $(adb_cmd "${serial}" shell wm size 2>/dev/null | tr -d '\r' | tail -n 1)"
}

start_emulator() {
  require_tools
  has_avd || fail "AVD '${AVD_NAME}' not found. Available AVDs: $(list_avds | tr '\n' ' ')"

  local serial
  serial="$(running_serial)"
  if [[ -n "${serial}" ]]; then
    echo "Using running Emulator ${serial}."
    doctor
    return 0
  fi

  local args=("-avd" "${AVD_NAME}" "-no-audio")
  if [[ "${UNICLAW_EMULATOR_HEADLESS:-0}" == "1" ]]; then
    args+=("-no-window")
  fi
  if [[ -n "${UNICLAW_EMULATOR_SNAPSHOT:-}" ]]; then
    args+=("-snapshot" "${UNICLAW_EMULATOR_SNAPSHOT}")
  fi
  if [[ -n "${UNICLAW_EMULATOR_GPU:-}" ]]; then
    args+=("-gpu" "${UNICLAW_EMULATOR_GPU}")
  fi

  local log_file="${UNICLAW_EMULATOR_LOG:-/tmp/uniclaw-emulator.log}"
  echo "Starting ${AVD_NAME} (log: ${log_file})"
  nohup "${EMULATOR_BIN}" "${args[@]}" >"${log_file}" 2>&1 </dev/null &
  echo "Emulator process: $!"

  serial="$(wait_for_device)" || fail "Emulator did not connect to ADB. See ${log_file}."
  wait_for_boot "${serial}" || fail "Emulator ${serial} did not finish booting. See ${log_file}."
  doctor
}

stop_emulator() {
  require_file "adb" "${ADB_BIN}"
  local serial
  serial="$(running_serial)"
  [[ -n "${serial}" ]] || { echo "No running Emulator found."; return 0; }
  adb_cmd "${serial}" emu kill
  echo "Stopped ${serial}."
}

case "${1:-}" in
  doctor) doctor ;;
  start) start_emulator ;;
  stop) stop_emulator ;;
  -h|--help|"") print_usage ;;
  *) print_usage >&2; exit 2 ;;
esac
