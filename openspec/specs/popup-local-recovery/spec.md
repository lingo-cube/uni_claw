# popup-local-recovery Specification

## Purpose
TBD - created by archiving change phase3-popup-local-recovery. Update Purpose after archive.

## Requirements

### Requirement: Treat supported Popup obstruction as Container scope
When external evidence indicates that a Popup or Overlay obstructs local interaction without proving that the underlying semantic page changed, the Runtime SHALL treat the condition as a Container-scope obstruction hypothesis. Container SHALL own the local semantic classification, while Environment SHALL provide only Observation and dispatch evidence and Traversal SHALL provide only local execution evidence.

#### Scenario: Popup does not immediately become Agent drift
- **WHEN** an active Container is valid, local execution is in progress, and an external Popup obstructs interaction without proving a semantic-page transition
- **THEN** the Runtime classifies the obstruction within Container scope and does not immediately rebind the Container or unconditionally initiate Agent recovery

### Requirement: Keep local obstruction handling bounded
Container SHALL authorize only approved bounded local handling through the existing Container → Traversal → Environment execution direction. Local handling SHALL NOT perform an unbounded or blind repeat, invoke Agent-scope restoration, or take ownership of the frozen Recovery component.

#### Scenario: Container attempts approved local dismissal
- **WHEN** supported local evidence identifies an actionable Popup dismissal within the current Container
- **THEN** the Runtime attempts only the approved bounded handling and obtains post-handling world evidence before any handled verdict

### Requirement: Verify Container continuity from a fresh Observation
After local obstruction handling, the Runtime SHALL obtain an Observation whose sequence strictly advances beyond the obstruction Observation. The Runtime SHALL consider local continuity proven only when the foreground application remains compatible, the active Container's existing `IsStillMine` rule accepts the fresh Observation, and reconciled semantic-page evidence does not contradict that Container.

#### Scenario: Dismissed Popup reveals the same Container
- **WHEN** bounded local handling removes the Popup and a fresh Observation satisfies the existing foreground, `IsStillMine`, and semantic-page continuity evidence
- **THEN** the Runtime treats the same logical Container as continuous and allows the existing execution protocol to continue

#### Scenario: Dispatch success alone does not prove continuity
- **WHEN** the Popup dismissal action reports a successful dispatch outcome but no fresh continuity evidence is available
- **THEN** the Runtime does not declare the obstruction handled or the Container continuous from the dispatch result alone

### Requirement: Preserve local progress on verified continuity
When Container continuity is proven after local obstruction handling, the Runtime SHALL preserve the same active Container and SHALL NOT clear, replace, or silently reset the local progress that existed before the obstruction. Successful local handling SHALL NOT itself satisfy GoalEvidence or complete the Run.

#### Scenario: Local work resumes without progress reset
- **WHEN** the Popup is removed and continuity of the active Container is proven
- **THEN** the same Container retains its pre-obstruction local progress, execution may continue, and final completion remains controlled by Agent evaluation of GoalEvidence

### Requirement: Escalate when local continuity cannot be proven
If bounded handling cannot be performed, the post-handling Observation is absent or stale, the foreground application is incompatible, `IsStillMine` rejects the fresh Observation, or reconciled semantic-page evidence remains Unknown or conflicting, Container SHALL NOT fabricate local recovery success. Container SHALL preserve available local progress and explicitly escalate structured evidence to Agent. Agent SHALL exclusively decide whether to rebind, initiate Agent recovery, or fail the Run.

#### Scenario: Dismissal or continuity verification fails
- **WHEN** the Popup cannot be dismissed or the fresh Observation cannot prove that the active Container remains valid
- **THEN** the Runtime performs no blind local repeat, reports the failed local proof to Agent, and leaves the higher-scope response and final Run state to Agent

### Requirement: Preserve frozen architecture and production vocabulary
SC-P3-002 SHALL preserve the Phase 2 Recovery ownership split, Agent recovery authority, Agent ownership of active Container transitions and final Run state, and the Recovery → Container/Traversal prohibition. The capability SHALL use existing production vocabulary and SHALL NOT require a new production model type, field, enum value, interface, component, or mutable state.

#### Scenario: Existing evidence surfaces prove both branches
- **WHEN** deterministic positive and escalation inputs for SC-P3-002 are executed
- **THEN** existing Observation, WorldBelief, Container identity and progress, Trap, journal, Trace, GoalEvidence, and RunState surfaces prove the behavior without a Popup model, manager, recovery engine, planner, FSM, Fingerprint, new Confidence mechanism, generic retry, or generic uncertainty abstraction

### Requirement: Replay Popup handling deterministically
The Runtime SHALL produce deterministic evidence for SC-P3-002 when RunId, input Environment, and action sequence are equal.

#### Scenario: Equal Popup inputs replay equally
- **WHEN** the same SC-P3-002 input is executed twice with equal RunId, Environment transitions, and action sequence
- **THEN** ActionHistory, Observation sequence, journal, Trace, continuity evidence, preserved progress, GoalEvidence, and final Run state are equal
