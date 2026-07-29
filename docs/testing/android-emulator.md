# Android Emulator integration

UniClaw's first real-system integration boundary is the Android Emulator plus
ADB. The current product fixture is the built-in AOSP Settings package
`com.android.settings`; no external APK installation is required. Device
readiness and Host scenario execution remain explicit opt-in operations.

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

# Run the project environment check against an already-running emulator.
# This never starts an emulator.
scripts/dev-doctor.sh --emulator
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

Automated tests and health checks should call `doctor` or
`scripts/dev-doctor.sh --emulator`. Use `start` only when the task explicitly
needs to launch a visible or headless AVD.

## Host Settings smoke

Start and verify the fixed fixture, then run the deterministic provider:

```bash
UNICLAW_EMULATOR_HEADLESS=1 scripts/android-emulator.sh start

dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  doctor --device emulator-5554 --provider mock --model deterministic-ui

dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  analyze --device emulator-5554 --provider mock --model deterministic-ui

dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  run --scenario scenarios/android-settings/locate-one-item.v1.json \
  --device emulator-5554 --provider mock --model deterministic-ui \
  --output artifacts/runs

# Optional real Sensenova (日日新) vision provider. The Host reads
# SENSENOVA_API_KEY or ~/.litellm/secrets.json and does not need Anthropic keys.
dotnet run --project src/UniClaw.Host/UniClaw.Host.csproj -- \
  analyze --device emulator-5554 --provider sensenova \
  --model sensenova-6.7-flash-lite --output artifacts/runs/commands

scripts/android-emulator.sh stop
```

Logical output layout:

```text
artifacts/runs/<scenario-id>/<run-id>/
  manifest.json
  scenario.snapshot.json
  plan.json
  steps/<nnnn>/{before,after,analysis,step-plan,safety-decision,verification}.*
  trace/<run-id>/{session.json,trace.jsonl}
  issues.jsonl
  result.json
```

`doctor` and `analyze` are read-only. `run` validates and snapshots the scenario
before action, resets to a verified Settings home, and routes every real action
through the deterministic safety gate.

## Failure triage and current boundary

- `device`/ADB failures are not no-scroll or end-of-list.
- `blocked` means the safety gate denied progress and the inner executor was not
  called.
- `failure` after a click means target-page verification did not match; inspect
  the correlated step assets and issue fingerprint. On the API 35 About-device
  page, UIAutomator can remain non-idle while the visible screenshot has already
  transitioned. The runner records `target_page_visual_transition_verified` only
  when the target row was safety-allowed and executed, the app boundary still
  holds, and the before/after PNG sizes differ by at least 20%; otherwise the
  mismatch remains a failure.
- A detached emulator reclaimed during entry polling may currently appear as an
  entry timeout; confirm `adb devices -l` before interpreting it.
- On the pinned API 35 fixture, safety-gated navigation to `System` and the
  bottom `About emulated device` row have been verified with the deterministic
  provider. Sensenova real vision is supported; its calls can take roughly
  30 seconds per screenshot, so the bounded locate scenario may finish as
  `incomplete:duration_budget_exhausted` even when device actions and safety
  decisions are healthy. First-level enumeration and repeat/stability gates
  remain deferred.
