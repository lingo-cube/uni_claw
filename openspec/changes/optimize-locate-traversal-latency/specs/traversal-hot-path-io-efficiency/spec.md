## ADDED Requirements

### Requirement: Step captures are produced once and reused by evidence hooks

During a traversal step, the device screen state (screenshot bytes and UIAutomator hierarchy XML) SHALL be captured at most once per observation point and made available to all consumers on the step context. Evidence hooks (e.g., RunAssetHook) SHALL consume the captures already produced by the step's page analysis and SHALL NOT issue their own ADB screencap or uiautomator dump for the same observation point.

#### Scenario: Hook reuses the analysis capture

- **WHEN** a traversal step performs page analysis that captures a screenshot and hierarchy XML
- **THEN** the step's before/after evidence hook consumes those exact bytes for `before.png`/`before.xml` or `after.png`/`after.xml` without issuing additional ADB commands

#### Scenario: No analysis on the step

- **WHEN** a step performs no page analysis (no capture exists on the step context)
- **THEN** the evidence hook captures the screen state itself rather than omitting evidence

### Requirement: Step asset writes do not block the traversal step loop

Evidence asset writes (screenshot and XML files) SHALL be submitted without awaiting durable completion on the traversal critical path. The run SHALL preserve evidence durability before run finalization.

#### Scenario: Asset write is deferred

- **WHEN** a step completes and its evidence hook submits before/after assets
- **THEN** the step loop continues without waiting for the file writes to reach disk

#### Scenario: Run finalization awaits durable assets

- **WHEN** the run reaches success, failure, or cancellation finalization
- **THEN** all accepted asset writes are drained and flushed before the run result is recorded as successful

### Requirement: Boundary package checks are fingerprint-gated

The foreground-package boundary hook SHALL run its `dumpsys` check only when the page hierarchy fingerprint changed since the last check, or at an explicit sampling interval. The first check after run start SHALL always run.

#### Scenario: Unchanged page skips boundary check

- **WHEN** a step completes and the hierarchy fingerprint equals the previously checked fingerprint
- **THEN** the boundary hook does not issue an ADB `dumpsys` call for that step

#### Scenario: Changed page triggers boundary check

- **WHEN** a step completes with a hierarchy fingerprint different from the previously checked one
- **THEN** the boundary hook issues its ADB `dumpsys` call and records any package violation
