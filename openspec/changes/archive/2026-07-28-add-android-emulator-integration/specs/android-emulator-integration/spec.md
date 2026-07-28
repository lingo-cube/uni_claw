## ADDED Requirements

### Requirement: Project discovers the canonical Android Emulator

The project SHALL provide a command that resolves the Android SDK, ADB, Emulator binary, and configured AVD without downloading or creating host resources implicitly. The command SHALL allow environment overrides for the SDK root and AVD name.

#### Scenario: Tools and AVD are available

- **WHEN** the discovery command runs with a valid SDK root and configured AVD
- **THEN** it reports the resolved SDK, ADB, Emulator, and AVD locations and exits successfully

#### Scenario: A required tool is missing

- **WHEN** the discovery command cannot find the SDK, ADB, Emulator, or requested AVD
- **THEN** it exits non-zero and prints the missing item plus an actionable remediation

### Requirement: Local and CI lifecycle modes

The project SHALL provide a start command that launches the configured AVD with a visible window by default. The command SHALL support an explicit headless mode for CI and SHALL provide a stop command that terminates only the selected Emulator through its ADB connection.

#### Scenario: Visible local startup

- **WHEN** a developer starts a configured AVD without headless mode
- **THEN** the Android Emulator window is visible and the command waits until the device is ADB-ready

#### Scenario: Headless startup

- **WHEN** CI starts the same AVD with headless mode enabled
- **THEN** the Emulator starts without a window and the command still waits for ADB readiness

#### Scenario: Stop selected device

- **WHEN** the stop command targets a running selected Emulator
- **THEN** it sends an ADB emulator shutdown only to that device and exits successfully

### Requirement: Device capability readiness probe

The project SHALL provide a doctor/probe command that fails before application integration when the selected Emulator is not ready for UniClaw Device operations. The probe SHALL verify boot completion, non-empty screenshot capture, and UIAutomator XML output.

#### Scenario: Ready device

- **WHEN** the selected Emulator reports boot completion and all capability probes succeed
- **THEN** the command exits successfully and reports the device serial and screen dimensions when available

#### Scenario: Boot incomplete or ADB unavailable

- **WHEN** the selected Emulator is absent, unauthorized, or not boot-complete before the timeout
- **THEN** the command exits non-zero with the serial/state and the failed readiness check

#### Scenario: Screenshot or UIAutomator probe fails

- **WHEN** screencap returns empty/invalid output or UIAutomator does not produce readable XML
- **THEN** the command exits non-zero and includes the failed command output for diagnosis

### Requirement: App configuration remains external

The project SHALL NOT require a hard-coded APK path or package name for Emulator discovery and readiness. When optional APK/package variables are supplied, the probe SHALL validate the APK installation and package visibility without changing the default app-agnostic flow.

#### Scenario: No target app configured

- **WHEN** the probe runs without APK/package variables
- **THEN** it validates only the device capabilities and exits without pretending an app integration test ran

#### Scenario: Target app configured

- **WHEN** a valid APK path and package name are supplied
- **THEN** the probe verifies the APK path and that the package is installed or reports the missing installation explicitly
