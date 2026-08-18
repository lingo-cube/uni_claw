#!/usr/bin/env bash
# capture.sh — launch a scenario, wait for stable UI, capture screenshot +
# uiautomator dump. Durable evidence saved under tools/android-runtime-reality-fixture/reality-evidence/.
#
# Usage:
#   capture.sh SCROLL_01 v1
#   capture.sh SCROLL_01 v2 --scroll-forward   (small swipe before capture)
#   capture.sh POPUP_02 before
#   capture.sh POPUP_02 waiting --delay 500
#   capture.sh POPUP_02 popup  --delay 2500
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ADB="${ADB:-adb}"
SERIAL="${ANDROID_SERIAL:-emulator-5554}"
SCENARIO="${1:?scenario id required}"
STEP="${2:?step name required}"
DELAY_MS=800
EXTRA_ARGS=()

while [ $# -gt 0 ]; do
    case "$1" in
        --delay) DELAY_MS="$2"; shift 2 ;;
        --scroll-forward)
            "$ADB" -s "$SERIAL" shell input swipe 540 1500 540 900 200
            shift ;;
        --tap) "$ADB" -s "$SERIAL" shell input tap "$2" "$3"; shift 3 ;;
        *) shift ;;
    esac
done

OUT="$ROOT/reality-evidence/$SCENARIO"
mkdir -p "$OUT"

# Ensure the scenario is foreground.
"$ADB" -s "$SERIAL" shell am start -n "com.uniclaw.fixture/.MainActivity" --es scenario "$SCENARIO" >/dev/null 2>&1 || true
HOST="com.uniclaw.fixture.ScrollActivity"
case "$SCENARIO" in
    SCROLL*) HOST="com.uniclaw.fixture.ScrollActivity" ;;
    POPUP*)  HOST="com.uniclaw.fixture.PopupActivity" ;;
    NAV*)    HOST="com.uniclaw.fixture.NavActivity" ;;
    COMPOSE*)HOST="com.uniclaw.fixture.ComposeActivity" ;;
esac
"$ADB" -s "$SERIAL" shell am start -n "com.uniclaw.fixture/.${HOST##*.}" --es scenario "$SCENARIO" >/dev/null

sleep "$(awk "BEGIN{print $DELAY_MS/1000}")"

"$ADB" -s "$SERIAL" exec-out screencap -p > "$OUT/$STEP.png"
"$ADB" -s "$SERIAL" shell uiautomator dump /sdcard/fixture_dump.xml >/dev/null
"$ADB" -s "$SERIAL" shell cat /sdcard/fixture_dump.xml > "$OUT/$STEP.xml"

echo "captured: $OUT/$STEP.png + $OUT/$STEP.xml"
