## Why

UniClaw already has ADB-backed screen capture, UIAutomator state inspection, and action execution, but there is no repeatable project entry point that discovers a local Android Emulator and verifies it is usable. The current development machine has ADB but no configured Emulator/AVD, so real-system integration cannot start consistently.

## What Changes

- Define a supported local Android Emulator profile for the current Intel macOS development host using an available lightweight API 35 x86_64 image.
- Add project-owned start/stop/doctor commands that discover the Android SDK, launch a visible AVD by default, and support headless CI mode.
- Add readiness checks for ADB, boot completion, screenshot capture, and UIAutomator XML output.
- Keep target App package/APK configuration external; do not hard-code an application that is not present in this repository.
- Document the boundary between emulator provisioning, device capability checks, and future UniClaw traversal-host composition.

## Capabilities

### New Capabilities

- `android-emulator-integration`: Project-level discovery, lifecycle, and capability checks for a local Android Emulator used by UniClaw device integration tests.

### Modified Capabilities

<!-- No existing requirement changes; this change adds host tooling around the existing Device and Simulation seams. -->

## Impact

- `scripts/` or equivalent project tooling for SDK/ADB/Emulator discovery and lifecycle commands.
- `docs/` documentation for the canonical AVD profile and local/CI usage.
- Existing `src/UniClaw.Device` implementations remain the integration seams; no Domain enum or Core interface changes are planned.
- Local developer environment: JDK 17+, Android command-line tools, Emulator package, and one x86_64 AVD are required.
- Future real-App traversal composition remains out of scope until an APK/package and Vision provider configuration are supplied.
