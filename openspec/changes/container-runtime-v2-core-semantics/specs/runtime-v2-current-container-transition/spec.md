## Purpose

Define the sole current physical working-Container projection, immutable transition occurrences, entry context, path-relative return, and atomic fresh-evidence reconciliation without copying pending execution obligations into current-location truth.

## ADDED Requirements

### Requirement: CurrentContainer is the sole Agent-owned current-location projection
The Runtime SHALL represent the current physical working location with exactly one Agent-owned CurrentContainer containing a NodeRef, CurrentSlice reference, and optional EntryContext. CurrentContainer SHALL NOT copy the node LocalModel, Graph identity truth, complete execution history, pending traversal obligation, or completion state.

#### Scenario: Fresh destination differs from pending obligation
- **WHEN** fresh accepted evidence establishes Settings Root while an incomplete Display traversal obligation remains pending
- **THEN** CurrentContainer SHALL reference a Settings Root working node and the Display obligation SHALL remain separate without automatic recovery, re-entry, action, or completion

#### Scenario: No parallel active truth
- **WHEN** CurrentContainer is committed
- **THEN** no mutable Graph-current, ActiveContainer-current, belief-current, or latest-transition slot SHALL independently claim a different current physical Container

### Requirement: EntryContext makes return path-relative
CurrentContainer EntryContext SHALL reference the actual Source node and entry TransitionOccurrence or relation evidence for this entry. A node SHALL NOT have a canonical parent. Return target SHALL be derived from the current entry/execution path and Back SHALL create an expectation that requires fresh observation verification.

#### Scenario: Same Settings node entered from Desktop
- **WHEN** Settings is entered from Desktop and Back is executed
- **THEN** the return expectation SHALL target Desktop and fresh evidence SHALL determine the observed return truth

#### Scenario: Same Settings node entered from Search
- **WHEN** the same Settings node is entered from Search and Back is executed
- **THEN** the return expectation SHALL target Search rather than a canonical Settings parent

#### Scenario: Entry relation is not synthesized return relation
- **WHEN** a forward entry relation exists from Source to Destination
- **THEN** the Runtime SHALL NOT prove a Back/return relation by reversing the forward relation

### Requirement: TransitionOccurrence records what physically occurred
For each accepted correlated transition observation, the Runtime SHALL produce an immutable TransitionOccurrence referencing Source NodeRef, trigger occurrence, observed Destination NodeRef when present, fresh observation revision, and outcome. A TransitionOccurrence SHALL remain distinct from a Graph relation and from the action expectation that preceded it.

#### Scenario: Many occurrences support one relation
- **WHEN** three independent actions and fresh observations enter the same destination through the same relation
- **THEN** three TransitionOccurrences SHALL remain recorded even if they support one derived Graph relation

#### Scenario: Action expectation differs from observation
- **WHEN** an action expects same-Container behavior but fresh evidence establishes a different destination
- **THEN** the occurrence SHALL record the observed destination and SHALL NOT be rewritten to match the expectation

### Requirement: Transition completion does not require identity trust
When fresh accepted evidence is sufficient to establish an independent working destination, the Runtime SHALL complete the TransitionOccurrence and update CurrentContainer even when the destination remains `INITIALIZED`, not Fast-trusted, and not Slow-confirmed.

#### Scenario: Independent unresolved destination completes transition
- **WHEN** correlated fresh evidence establishes a separate destination working node with unresolved semantic identity
- **THEN** the transition SHALL be completed, CurrentContainer SHALL reference that node, and identity trust SHALL remain unresolved

### Requirement: Reconciliation is revision-bound and atomic
The Runtime SHALL validate the candidate fresh observation, CurrentContainer replacement, EntryContext, TransitionOccurrence, permitted existing local evidence update, and permitted obligation/progress evidence before one synchronous commit. A stale, contradictory, or invalid candidate SHALL commit none of these values.

#### Scenario: Stale semantic result arrives
- **WHEN** an assessment references an older observation or transition revision than CurrentContainer
- **THEN** it SHALL NOT modify the current world projection, Graph assessment, or pending obligation

#### Scenario: Commit validation fails
- **WHEN** the candidate occurrence or entry context is internally inconsistent
- **THEN** CurrentContainer, local evidence, Graph evidence, and Agent obligation state SHALL remain unchanged and the rejection SHALL be explicit

### Requirement: Current location and obligation updates preserve authority boundaries
Recording CurrentContainer or a TransitionOccurrence SHALL NOT select a next sibling, authorize an action, execute recovery, infer subtree completion, or create GoalEvidence. Those decisions SHALL remain with their existing Agent/UniAgent owners.

#### Scenario: Unexpected accepted destination
- **WHEN** an unexpected destination becomes CurrentContainer
- **THEN** the Runtime SHALL expose the occurrence and unresolved/corrected semantic facts while requiring a separate Agent/UniAgent decision for what happens next

### Requirement: Live compatibility views derive one-way from V2 current evidence
When the live state replacement is enabled, `ContainerRuntimeV2State.CurrentContainer` SHALL be the sole Agent-owned physical-current projection. `Agent.Belief` and legacy typed transition/read fields MAY remain as compatibility views only when derived from the same accepted V2 node, Slice, evidence revision, and TransitionOccurrence. They SHALL NOT retain independent mutable write paths or reconcile back into V2 state.

#### Scenario: Belief compatibility cannot diverge
- **WHEN** a fresh accepted V2 occurrence advances CurrentContainer to a new working node
- **THEN** `Agent.Belief` SHALL project the semantic candidate and observation evidence from that V2 state and no `_belief` mutable field SHALL be independently assigned

#### Scenario: Legacy transition is audit compatibility only
- **WHEN** a V2 TransitionOccurrence is accepted
- **THEN** any legacy `ContainerTransition` trace record SHALL be produced from that occurrence and its existing expectation/context inputs, SHALL remain append-only audit evidence, and SHALL NOT become current occurrence truth

### Requirement: Initial live replacement enables Fast and disables Slow
The first live production slice SHALL run the revision-bound Fast resolver through the stateless `ContainerRuntimeV2` facade and SHALL use `SlowContainerSemanticMode.Disabled`. It SHALL NOT bind a Slow provider, wait for Slow, or store mutable latest assessment/trust/correction/checkpoint state.

#### Scenario: Fast-only initial production slice
- **WHEN** an initial or post-action fresh observation is accepted during the bounded live replacement
- **THEN** V2 state and the Fast assessment SHALL be produced from one evidence context, Slow SHALL report Disabled, and existing Agent obligation/action/Goal/recovery behavior SHALL remain unchanged
