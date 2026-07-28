# Android Emulator integration

UniClaw's first real-system integration boundary is the Android Emulator plus
ADB. This repository does not contain a target Android application, APK, or
package name, so the current tooling validates the device boundary only. A
future change can add APK installation and `TraversalEngine`/Vision composition
once a target app is selected.

## Supported local profile

The default profile is intentionally small and visible:

```text
AVD: uniclaw-lite-api35
System image: API 35, default, x86_64
Host: Intel x86_64 macOS (use arm64-v8a on Apple Silicon)
Window: visible by default
```

Use a `google_apis` image instead when the target APK requires Google Play
Services. Keep that choice in `UNICLAW_AVD_NAME`; the project script does not
download or create AVDs implicitly.

The host needs JDK 17+, Android command-line tools, `platform-tools`, and the
`emulator` package. Create the AVD once with `avdmanager`, then run the project
script from the repository root.

## Commands

```bash
# Check SDK, ADB, Emulator, AVD, boot state, screenshot, and UIAutomator.
scripts/android-emulator.sh doctor

# Start a visible emulator window and run the readiness probe.
scripts/android-emulator.sh start

# CI/headless mode; the same AVD and probes are used.
UNICLAW_EMULATOR_HEADLESS=1 scripts/android-emulator.sh start

# Stop only the selected emulator through ADB.
scripts/android-emulator.sh stop
```

Useful overrides:

```bash
ANDROID_SDK_ROOT=/path/to/android-sdk \
UNICLAW_AVD_NAME=my-avd \
scripts/android-emulator.sh doctor
```

If the default ADB server port 5037 is occupied on macOS, use another port for
both startup and checks:

```bash
UNICLAW_ADB_SERVER_PORT=5038 scripts/android-emulator.sh start
```

For an optional target App, provide both values. The doctor command only checks
that the APK exists and the package is installed; it does not install an APK or
claim that a traversal test ran.

```bash
UNICLAW_APK_PATH=/path/to/app.apk \
UNICLAW_PACKAGE=com.example.app \
scripts/android-emulator.sh doctor
```

The script verifies `sys.boot_completed`, a non-empty `adb exec-out screencap`
result, and readable `uiautomator dump` XML. These checks correspond to the
existing `UniClaw.Device` seams in `AdbScreenCapture`,
`AdbScreenStateProvider`, and `AdbActionExecutor`.

## Boundary to future integration

This tooling does not modify Core interfaces or the in-memory simulation. The
next device-integration change should add an explicit target App configuration,
an executable composition root, and a `DeviceIntegration` test category that
constructs the ADB implementations with the selected Vision provider.
