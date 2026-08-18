# uncertain-action-verification Specification

## Purpose
TBD - created by archiving change phase3-uncertain-action. Update Purpose after archive.

## Requirements

### Requirement: Verify a TimedOut action from a fresh Observation
When dispatch of a grounded action returns `ActionResultOutcome.TimedOut`, the Runtime SHALL treat the dispatch outcome as uncertain rather than as proof of success or confirmed world failure. The Runtime SHALL obtain a fresh post-action Observation before advancing its verdict, and SHALL NOT automatically dispatch the same action again.

#### Scenario: Click effect is observable after dispatch timeout
- **WHEN** a grounded Click changes the external world but its dispatch returns `TimedOut`
- **THEN** the Runtime obtains a fresh Observation, continues through the existing verification and world-evidence flow, and dispatches that Click exactly once

#### Scenario: Click effect is not observable after dispatch timeout
- **WHEN** a grounded Click returns `TimedOut` and the fresh Observation does not verify the intended local world effect
- **THEN** the Runtime does not fabricate action success or Goal success and does not blindly dispatch the Click again

### Requirement: Preserve existing completion authority
A `TimedOut` result and successful local continuation SHALL NOT complete the Run. The Run SHALL enter `Completed` only when the existing Goal evidence evaluator produces satisfied `GoalEvidence` from Observation evidence.

#### Scenario: Local effect is verified but Goal is not yet complete
- **WHEN** the post-timeout Observation permits local execution to continue but does not satisfy the Goal evidence evaluator
- **THEN** the Run remains non-complete and continues under the existing Agent authority

### Requirement: Keep post-dispatch uncertainty separate from pre-dispatch retry
The Runtime SHALL NOT reuse SC-P2-002 pre-dispatch target retry as permission to repeat an action after `TimedOut`. SC-P2-002 performs re-observe and re-resolve before any action dispatch; SC-P3-001 assumes the action may already have been dispatched and may already have changed the external world.

#### Scenario: TimedOut does not enter blind Step retry
- **WHEN** an action has been submitted and dispatch returns `TimedOut`
- **THEN** the Runtime observes the external world before any further action decision and does not increment or execute the existing pre-dispatch retry loop as a duplicate dispatch mechanism

### Requirement: Preserve the frozen production model
SC-P3-001 SHALL be expressed with the existing `ActionResult`, `Observation`, `TraversalStepResult`, Traversal journal, Trace, WorldBelief, and GoalEvidence vocabulary. The capability SHALL NOT require a new production model type, field, enum value, interface, component, or mutable state.

#### Scenario: Existing evidence surfaces prove deterministic behavior
- **WHEN** the same SC-P3-001 deterministic input is executed twice
- **THEN** existing ActionHistory, Observation sequence, journal, Trace, GoalEvidence, and final Run state surfaces provide equal evidence without a new production model surface
