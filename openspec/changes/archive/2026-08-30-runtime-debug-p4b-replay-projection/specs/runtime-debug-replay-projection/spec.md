## ADDED Requirements

### Requirement: Deterministic dry-run projection
The Toolchain SHALL project one validated replay fixture into an ordered trajectory (step order/kind/sequence/action facts/result outcome/frame), counts (steps/observations/actions/last observation sequence), and the first mechanically non-OK step (resultOutcome outside {Dispatched, Succeeded}). The projection SHALL be read-only, deterministic, and perform no state simulation; minimization SHALL remain reserved.

#### Scenario: Rejected result surfaces as first mechanical failure
- **WHEN** a fixture's step order 3 has resultOutcome Rejected
- **THEN** the projection SHALL report firstMechanicallyFailedStep=3 with the full ordered trajectory

#### Scenario: Clean fixture has no mechanical failure
- **WHEN** every result outcome is Dispatched or Succeeded
- **THEN** firstMechanicallyFailedStep SHALL be null

### Requirement: Fail-closed projection input
The projection SHALL validate the fixture before projecting; missing files SHALL fail closed with `EVIDENCE_UNAVAILABLE` and malformed fixtures with `SCHEMA_VIOLATION`.

#### Scenario: Missing fixture fails closed
- **WHEN** the fixture file does not exist
- **THEN** the command SHALL return `EVIDENCE_UNAVAILABLE` without a trajectory
