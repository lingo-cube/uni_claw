# semantic-run-popup-obstruction-integration Specification

## Purpose
TBD - created by archiving change semantic-run-popup-obstruction-integration. Update Purpose after archive.

## Requirements

### Requirement: Local obstruction detection in SemanticRun

The SemanticRun loop SHALL detect local obstructions using the existing `Container.IsLocalObstructionHypothesis` mechanism when a fresh Observation does not match the expected semantic page but the foreground application is unchanged.

#### Scenario: Popup appears during active Goal

- **WHEN** an active semantic Goal is running and an unexpected popup/overlay appears
- **THEN** SemanticRun SHALL detect the local obstruction hypothesis
- **AND** SHALL NOT execute the intended semantic action through the popup

### Requirement: Bounded obstruction handling

When a local obstruction is detected, SemanticRun SHALL attempt bounded handling using an existing dismiss/back action, then SHALL obtain a fresh Observation and verify the obstruction is cleared before continuing the same Goal.

#### Scenario: Dismiss succeeds

- **WHEN** a dismiss action is dispatched and a fresh Observation confirms the original semantic context
- **THEN** SemanticRun SHALL reconcile the Container and continue the SAME Goal

#### Scenario: Dismiss fails

- **WHEN** a dismiss action is dispatched but the popup remains or the Observation is ambiguous
- **THEN** SemanticRun SHALL fail closed without executing stale actions

### Requirement: Stale grounding rejection

After a local obstruction is detected and handled, pre-obstruction ElementIndex, Bounds, and binding grounding SHALL NOT be reused for subsequent semantic actions. Fresh grounding from the post-obstruction Observation is required.

#### Scenario: Pre-popup grounding not reused

- **WHEN** a popup is dismissed and the original page returns
- **THEN** the next semantic action SHALL use fresh binding derived from the post-dismiss Observation

### Requirement: Same Goal preservation

Successful local-obstruction recovery SHALL preserve the SemanticGoalInput, DesiredValue, and semantic Goal identity.

#### Scenario: Goal survives after popup recovery

- **WHEN** a popup is dismissed and the original semantic context is restored
- **THEN** the same SemanticGoal SHALL continue with the same ObjectIdentity, StateDimension, and DesiredValue

### Requirement: Normal path unchanged

If no local obstruction is present, SemanticRun SHALL behave exactly as before.

#### Scenario: No popup

- **WHEN** no local obstruction exists
- **THEN** no additional recovery behavior SHALL occur
