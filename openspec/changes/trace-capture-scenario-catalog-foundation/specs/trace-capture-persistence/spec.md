## Purpose

Defines deterministic, append-only, atomic local persistence for immutable TraceCapture bundles and their content-addressed artifacts.

## ADDED Requirements

### Requirement: Capture publication is atomic and append-only
Persistence SHALL validate a capture in staging and publish it atomically. An existing CaptureSessionId MUST NOT be overwritten or mutated.

#### Scenario: Complete capture publishes once
- **WHEN** a valid capture bundle is saved under a new CaptureSessionId
- **THEN** one complete immutable capture directory becomes visible with deterministic paths and hashes

#### Scenario: Existing capture ID is refused
- **WHEN** a save targets an already published CaptureSessionId
- **THEN** persistence fails closed and leaves the published capture unchanged

### Requirement: Incomplete captures are not admitted automatically
Failed, cancelled, incomplete, or unreviewed raw captures SHALL NOT be automatically added to the repository Scenario catalog.

#### Scenario: Persistence fails before publication
- **WHEN** staging validation, hashing, cancellation, or publication fails
- **THEN** no partial catalog candidate becomes visible and diagnostic preservation remains quarantined
