#!/usr/bin/env bash
# build.sh — manual Android build for the reality fixture app (NO Gradle).
# Requires: JDK 17, Android SDK cmdline-tools + platforms;android-35 + build-tools;35.0.0
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK="${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}"
BT="$SDK/build-tools/35.0.0"
ANDROID_JAR="$SDK/platforms/android-35/android.jar"
JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@17}"

AAPT2="$BT/aapt2"
D8="$BT/d8"
ZIPALIGN="$BT/zipalign"
APKSIGNER="$BT/apksigner"
JAVAC="$JAVA_HOME/bin/javac"
KEYTOOL="$JAVA_HOME/bin/keytool"

BUILD="$ROOT/build"
KEYSTORE="$ROOT/build-keys/debug.keystore"
rm -rf "$BUILD"; mkdir -p "$BUILD/gen" "$BUILD/classes" "$BUILD/dex" "$ROOT/build-keys"

echo "[1/6] aapt2 compile resources"
"$AAPT2" compile --dir "$ROOT/res" -o "$BUILD/res.zip"

echo "[2/6] aapt2 link (manifest + resources + R.java)"
"$AAPT2" link -o "$BUILD/base.apk" \
    -I "$ANDROID_JAR" \
    --manifest "$ROOT/AndroidManifest.xml" \
    -R "$BUILD/res.zip" \
    --java "$BUILD/gen" \
    --auto-add-overlay

echo "[3/6] javac"
find "$BUILD/gen" "$ROOT/src" -name '*.java' > "$BUILD/sources.txt"
"$JAVAC" -source 8 -target 8 -classpath "$ANDROID_JAR" \
    -d "$BUILD/classes" @"$BUILD/sources.txt"

echo "[4/6] d8 -> classes.dex"
find "$BUILD/classes" -name '*.class' > "$BUILD/classes.txt"
"$D8" --release --lib "$ANDROID_JAR" --output "$BUILD/dex" @"$BUILD/classes.txt"

echo "[5/6] package classes.dex + zipalign"
(cd "$BUILD/dex" && zip -q -u "$BUILD/base.apk" classes.dex)
"$ZIPALIGN" -f 4 "$BUILD/base.apk" "$BUILD/aligned.apk"

echo "[6/6] sign"
if [ ! -f "$KEYSTORE" ]; then
    "$KEYTOOL" -genkeypair -keystore "$KEYSTORE" \
        -alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 \
        -storepass android -keypass android -dname "CN=Android Debug,O=Android,C=US"
fi
"$APKSIGNER" sign --ks "$KEYSTORE" --ks-pass pass:android \
    --key-pass pass:android --out "$BUILD/fixture-debug.apk" "$BUILD/aligned.apk"

echo "APK: $BUILD/fixture-debug.apk"
ls -la "$BUILD/fixture-debug.apk"
