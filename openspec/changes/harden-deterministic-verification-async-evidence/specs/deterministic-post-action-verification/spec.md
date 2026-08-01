## ADDED Requirements

### Requirement: Locate completion verification is provider-free after the target action

After a locate target action succeeds, the Host SHALL verify completion without invoking `IPageAnalyzer`, a visual model provider, or any other remote AI capability. It SHALL use a stabilized screenshot plus deterministic UIAutomator hierarchy/title evidence from the selected device.

#### Scenario: Successful target navigation performs no final model call
- **WHEN** the target click succeeds and the destination exposes a trusted Settings toolbar/title matching a configured target identity or alias
- **THEN** the Host verifies success from UIAutomator, captures the final screenshot, and the provider call count does not increase after the target action

#### Scenario: Deterministic verification is unavailable
- **WHEN** the final UIAutomator hierarchy cannot be read or parsed within the verification budget
- **THEN** the run fails with a classified deterministic-verification/device reason and MUST NOT fall back to a visual model call

### Requirement: Final page identity comes from a trusted UIAutomator title

The Host SHALL derive the final locate page identity only from a non-empty title associated with a configured trusted Settings toolbar/title resource. A generic fallback such as `Settings`, arbitrary visible text, coordinates, or model-generated path MUST NOT satisfy the target-page identity criterion.

#### Scenario: Trusted toolbar identity matches an alias
- **WHEN** UIAutomator reports `About emulated device` from a trusted toolbar resource and the scenario declares it as an alias
- **THEN** normalized identity matching succeeds and records `target_page_identity:About emulated device`

#### Scenario: Only arbitrary content text matches
- **WHEN** target-like text appears in page content but no trusted toolbar/title identity is available
- **THEN** completion remains unverified and the run MUST NOT report success

### Requirement: Final screenshot and hierarchy are stabilized correlated evidence

The Host SHALL collect final evidence within a bounded stabilization budget and persist the screenshot, UIAutomator XML, derived identity, hierarchy fingerprint, timestamp, run ID, and final step number with shared correlation. `result.json` SHALL reference the stabilized evidence paths rather than an immediate pre-transition frame.

#### Scenario: Android renders after the engine hook returns
- **WHEN** the immediate `OnAfterStep` screenshot still shows the source page but bounded stabilization reaches the destination
- **THEN** the referenced final `after.png` and `after.xml` are refreshed to the destination evidence before result finalization

#### Scenario: Destination never stabilizes
- **WHEN** the trusted identity does not stabilize before the verification timeout
- **THEN** the run reports `target_page_identity_not_verified` or a more specific verification timeout and preserves the last captured diagnostic evidence

### Requirement: Deterministic failure cannot be promoted by screenshot appearance alone

A screenshot SHALL be retained for human review but SHALL NOT by itself authorize an automated successful result. Success SHALL require a successful target action, a trusted deterministic identity match, and durable correlated evidence.

#### Scenario: Screenshot visually resembles the destination but title mismatches
- **WHEN** the bitmap appears to show the target page but the trusted UIAutomator title is missing or mismatched
- **THEN** automated completion fails and the screenshot remains diagnostic evidence only
