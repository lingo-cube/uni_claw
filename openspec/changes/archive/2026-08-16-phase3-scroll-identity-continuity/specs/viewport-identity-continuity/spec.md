## ADDED Requirements

### Requirement: Represent one bounded forward viewport movement
The Runtime SHALL represent one bounded forward viewport movement as exactly one immutable production action variant. The action SHALL require no element target or additional production field and SHALL NOT introduce direction, coordinate, distance, duration, progress, or repeated-scroll policy semantics.

#### Scenario: Dispatch a targetless viewport action
- **WHEN** an approved local plan step requests one bounded forward viewport movement
- **THEN** Traversal dispatches exactly one viewport action without fabricating an element target and records the action in the existing journal and Trace evidence surfaces

### Requirement: Obtain fresh post-movement evidence
After dispatching the viewport action, Traversal SHALL obtain a post-action Observation whose sequence strictly advances beyond the pre-action Observation. Dispatch outcome alone SHALL NOT prove viewport progress, Container continuity, or Goal completion, and the Runtime SHALL NOT blindly redispatch the action.

#### Scenario: Fresh Observation follows one viewport action
- **WHEN** the Environment accepts one bounded forward viewport action
- **THEN** Traversal observes exactly once, requires a strictly newer Observation, and exposes the post-action evidence through the existing execution protocol

#### Scenario: Stale evidence does not prove progress
- **WHEN** the post-action Observation is absent or does not advance beyond the pre-action sequence
- **THEN** the Runtime reports failure evidence, does not declare viewport progress or Container continuity, and does not blindly redispatch the action

### Requirement: Preserve Container identity across viewport change
A changed visible element set SHALL NOT by itself imply semantic navigation, Container replacement, PressBack, or Recovery. Container SHALL accept viewport continuity only when the fresh Observation has compatible foreground evidence, the existing `IsStillMine` rule accepts it, and reconciled semantic-page evidence does not contradict the active Container.

#### Scenario: Different visible elements remain in one Container
- **WHEN** Observation 1 exposes one element set, one bounded forward viewport movement occurs, and fresh Observation 2 exposes a different element set while existing identity evidence accepts the same semantic page
- **THEN** the Runtime preserves the same active Container and does not interpret snapshot change as navigation

#### Scenario: Snapshot difference is not identity authority
- **WHEN** visible elements differ between fresh Observations
- **THEN** the Runtime uses existing semantic identity evidence rather than snapshot equality, snapshot difference, or Fingerprint as the Container identity authority

### Requirement: Advance local Observation without resetting progress
When viewport continuity is proven, Container SHALL advance its owned current Observation to the fresh post-action evidence without invoking a progress-resetting bind. The same Container SHALL preserve all pre-movement local progress and may continue existing execution.

#### Scenario: Continue from the fresh viewport with preserved progress
- **WHEN** the active Container has recorded local progress and fresh post-movement evidence proves continuity
- **THEN** its current Observation advances, its pre-movement progress remains present, and execution may continue in the same Container

### Requirement: Escalate contradictory viewport evidence
If the viewport action is rejected, fresh evidence is unavailable, foreground is incompatible, `IsStillMine` rejects the Observation, or reconciled semantic-page evidence contradicts the active Container, Container SHALL NOT fabricate continuity or reset progress. It SHALL expose Container-scope evidence, while Agent SHALL exclusively decide rebind, Agent Recovery, failure, GoalEvidence, and final RunState.

#### Scenario: New semantic page is not accepted as local viewport continuity
- **WHEN** fresh post-action evidence resolves to a different semantic page or otherwise fails existing Container identity rules
- **THEN** the Runtime preserves available local progress, emits structured Container-scope evidence, performs no blind viewport repeat, and leaves the higher-scope response to Agent

### Requirement: Preserve the approved production and architecture boundary
SC-P3-003 SHALL add exactly one immutable action variant and no production field, enum, interface, component, or mutable state. Ownership and authority deltas SHALL remain none, Recovery SHALL NOT depend on Container or Traversal, and the capability SHALL NOT add Fingerprint, gesture geometry, scroll progress, reverse/repeated scrolling, end-of-list detection, generic scroll infrastructure, or Runtime refactoring.

#### Scenario: Minimum semantic purchase is sufficient
- **WHEN** deterministic positive and escalation branches execute
- **THEN** the approved action variant plus existing Observation, Container, Traversal journal, Trap, Trace, GoalEvidence, and RunState surfaces prove SC-P3-003 without any deferred production capability

### Requirement: Replay viewport continuity deterministically
The Runtime SHALL produce deterministic SC-P3-003 evidence when RunId, input Environment, and action sequence are equal.

#### Scenario: Equal viewport inputs replay equally
- **WHEN** the same SC-P3-003 input is executed twice with equal RunId, Environment transitions, and action sequence
- **THEN** ActionHistory, Observation sequence, journal, Trace, Container identity evidence, local progress, GoalEvidence, and final RunState are equal
