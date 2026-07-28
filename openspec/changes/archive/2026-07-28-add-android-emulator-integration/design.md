## Context

UniClaw.Device already contains ADB implementations for screenshot capture, UIAutomator state inspection, and actions. The repository has no Android application module and no fixed APK/package name, so the first integration slice must provision and validate a device without pretending to run a product-specific traversal.

The current development host is an Intel x86_64 Mac with 16 GB RAM. A visible emulator is required for local debugging; CI can use the same AVD in headless mode.

## Goals / Non-Goals

**Goals:**

- Provide one project-owned command entry point for discovering the Android SDK, ADB, Emulator binary, and canonical AVD.
- Start the canonical AVD with a visible window by default and support an explicit headless mode.
- Verify boot readiness, screenshot capture, and UIAutomator XML output before a future DeviceIntegration test uses the device.
- Keep APK/package configuration optional and external so the repository remains app-agnostic.
- Document the local setup and the boundary to future TraversalEngine/Vision host composition.

**Non-Goals:**

- Do not install SDK packages or mutate the host as part of a repository script.
- Do not add an Android app, APK, package name, Vision provider, or production composition root.
- Do not change Core interfaces, Domain enums, traversal behavior, or simulation baseline assertions.
- Do not make an Emulator prerequisite for the existing in-memory test suite.

## Decisions

1. **Use a shell entry point under `scripts/`**

   A shell script keeps the setup usable before a .NET device host exists and works from both a developer terminal and CI. It exposes `doctor`, `start`, and `stop` commands rather than embedding host-specific process logic in Core.

2. **Visible by default; headless by opt-in**

   `start` launches the AVD with its normal GUI. Setting `UNICLAW_EMULATOR_HEADLESS=1` adds `-no-window` for CI. This preserves the same AVD and ADB behavior in local and automated runs.

3. **Environment-driven discovery**

   Resolve `ANDROID_SDK_ROOT`, then `ANDROID_HOME`, then platform defaults. Resolve `UNICLAW_AVD_NAME` with a documented canonical default. Print actionable errors instead of silently downloading or creating anything.

4. **Capability probe before application tests**

   The doctor command checks `adb devices`, `sys.boot_completed`, a non-empty PNG from `screencap`, and parseable UIAutomator output. Optional `UNICLAW_APK_PATH` and `UNICLAW_PACKAGE` checks are only performed when supplied.

5. **No new Core layer**

   Provisioning is host tooling. The API 35 `default;x86_64` image is the available lightweight no-GMS image in the current SDK repository. The existing `src/UniClaw.Device` seams remain the future consumer, so no Tier 1 enum, Core interface, or dependency-direction changes are needed.

## Risks / Trade-offs

- **[Missing SDK/JDK]** → `doctor` reports the exact missing executable and setup variable; installation remains an explicit developer/CI step.
- **[AVD name differs by machine]** → allow `UNICLAW_AVD_NAME` override and show available AVDs in the error.
- **[Emulator boots but UIAutomator is unavailable]** → fail the probe before traversal and include the ADB command output.
- **[Target APK is ARM-only on Intel]** → keep APK validation optional and document that real ARM64 device coverage remains a later tier.
- **[GUI process lifecycle differs across shells]** → use ADB `emu kill` for stop and avoid destructive process-wide termination.

## Migration Plan

1. Install JDK 17+, Android command-line tools, the Emulator package, and the canonical x86_64 system image on the developer host.
2. Create the `UNICLAW_AVD_NAME` AVD and run the repository `doctor` command.
3. Use `start` for local visible debugging; use `UNICLAW_EMULATOR_HEADLESS=1 start` in CI.
4. Later add a separate change for APK installation and `TraversalEngine` composition once a target App and Vision provider are selected.

## Open Questions

- Which target APK/package will define the first true `DeviceIntegration` scenario?
- Should the canonical image be AOSP or `google_apis` once the target App's Play Services requirements are known?
- Which CI runner will host the headless Emulator and cache the system image?
