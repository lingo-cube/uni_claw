#!/usr/bin/env bash
# install.sh — install the fixture APK on emulator-5554 and launch a scenario.
# Usage: install.sh [SCENARIO_ID]   (e.g. install.sh SCROLL_01)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ADB="${ADB:-adb}"
SERIAL="${ANDROID_SERIAL:-emulator-5554}"
APK="$ROOT/build/fixture-debug.apk"

if [ ! -f "$APK" ]; then
    echo "APK missing — run scripts/build.sh first" >&2
    exit 1
fi

echo "[adb] targeting $SERIAL"
"$ADB" -s "$SERIAL" install -r "$APK"

if [ $# -ge 1 ]; then
    SCENARIO="$1"
    HOST="com.uniclaw.fixture.ScrollActivity"
    case "$SCENARIO" in
        SCROLL*) HOST="com.uniclaw.fixture.ScrollActivity" ;;
        POPUP*)  HOST="com.uniclaw.fixture.PopupActivity" ;;
        NAV*)    HOST="com.uniclaw.fixture.NavActivity" ;;
        COMPOSE*)HOST="com.uniclaw.fixture.ComposeActivity" ;;
    esac
    "$ADB" -s "$SERIAL" shell am start -n "com.uniclaw.fixture/.${HOST##*.}" \
        --es scenario "$SCENARIO"
    echo "[launch] $SCENARIO -> $HOST"
fi
