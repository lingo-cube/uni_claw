## 1. Project Emulator Tooling

- [x] 1.1 Add `scripts/android-emulator.sh` with SDK/ADB/Emulator/AVD discovery and `doctor`, `start`, and `stop` subcommands.
- [x] 1.2 Make visible startup the default and support `UNICLAW_EMULATOR_HEADLESS=1` for CI without changing the AVD profile.
- [x] 1.3 Implement boot, screenshot, and UIAutomator capability probes with bounded timeouts and actionable failures.
- [x] 1.4 Support optional external `UNICLAW_APK_PATH` and `UNICLAW_PACKAGE` validation without making them required.

## 2. Documentation and Configuration

- [x] 2.1 Document JDK 17+, Android SDK command-line tools, canonical AVD profile, environment overrides, and local/CI commands.
- [x] 2.2 Document that this slice validates the device boundary only; target APK/Vision/Traversal composition is a follow-up change.

## 3. Verification

- [x] 3.1 Verify shell syntax and missing-tool/AVD failure paths on the current host.
- [x] 3.2 Install the required host components, create the canonical AVD, and verify visible startup plus `doctor` readiness. Verified on macOS Intel with API 35 `default;x86_64`, visible launch, screenshot, and UIAutomator probes.
- [x] 3.3 Run the existing Simulation E2E tests to prove the new tooling does not affect in-memory simulation.

## Design Docs

> This change adds host tooling and does not alter a Core layer or existing simulation requirements.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Device/` | `docs/refactor/2026-07-15-vision-mode-strategy-design.md` |
| `tests/` | `docs/system/layers/simulation.md` + `docs/system/layers/simulation-baseline.md` |
