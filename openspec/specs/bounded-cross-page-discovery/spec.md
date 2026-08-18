# bounded-cross-page-discovery Specification

## Purpose
TBD - created by archiving change phase3-bounded-cross-page-discovery. Update Purpose after archive.

## Requirements

### Requirement: Represent bounded required-branch inventory evidence
The Runtime SHALL add exactly one immutable `BranchInventoryEvidence` production value with nullable immutable `RequiredBranchEvidence` and non-empty `Reason` fields. Each non-null map entry SHALL associate one required semantic branch identity with the accepted source Observation sequence that supports it. A non-null map SHALL mean the complete bounded required branch inventory is positively proven, an empty non-null map SHALL mean a bounded leaf is positively proven, and a null map SHALL mean inventory completeness is unresolved. The value SHALL NOT represent candidate authorization, a Plan, route state, branch completion, GoalEvidence, or Run completion.

#### Scenario: Non-empty empty and unresolved inventories remain distinct
- **WHEN** bounded accepted evidence proves required branches, positively proves a leaf, or cannot prove complete inventory
- **THEN** the value represents those outcomes as a non-empty map, empty non-null map, or null map respectively with a non-empty deterministic reason

### Requirement: Carry one optional inventory criterion on Goal
The Runtime SHALL add exactly one optional immutable `Goal.BranchInventoryEvaluator` field with semantic shape `Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>?`. The evaluator SHALL be deterministic and side-effect-free, SHALL consume only bounded accepted same-Container Observation evidence plus Agent-derived semantic depth and immutable captured Goal scope, and SHALL NOT call Environment, dispatch, mutate Runtime owners, authorize candidates, or set RunState.

#### Scenario: Existing fixed Plan behavior remains compatible
- **WHEN** Goal has no branch inventory evaluator
- **THEN** existing fixed-Plan behavior remains unchanged and no cross-page inventory decision is fabricated

#### Scenario: Criterion receives bounded current evidence
- **WHEN** Agent evaluates the current Container's required inventory
- **THEN** the evaluator receives immutable accepted same-Container evidence whose final Observation is current and fresh plus the current evidence-backed semantic depth

### Requirement: Establish inventory from accepted evidence rather than Plan
Agent SHALL remain the sole authority that consumes `BranchInventoryEvidence`. A non-null inventory SHALL be validated against the active semantic Container and the accepted source Observation sequences before Agent creates or refreshes the existing `BranchProgressEvidence` for that parent scope. Exact target presence in the initial Plan SHALL NOT be required to prove inventory. Plan presence alone SHALL NOT prove any branch. Null, stale, identity-conflicting, or ambiguously parented evidence SHALL NOT replace valid progress or fabricate an empty leaf.

#### Scenario: Fresh inventory is accepted without a pre-encoded route
- **WHEN** fresh accepted P evidence positively proves required branch A and A is absent from the initial Plan
- **THEN** Agent may establish P's required inventory from the evidence rather than rejecting it solely because Plan omitted A

#### Scenario: Unresolved inventory preserves prior evidence
- **WHEN** the evaluator returns null or references stale/conflicting evidence
- **THEN** Agent preserves valid prior progress, dispatches no discovered branch from that result, and does not claim a leaf or completion

### Requirement: Keep required membership separate from authorization
Agent SHALL select a discovered branch only when it is present in the proven required inventory, is not already proven complete, and the existing SC-P3-CAND-006 criterion independently returns authorized for its source Observation candidate. Required plus rejected or unresolved authorization SHALL produce zero dispatch and explicit unresolved route evidence. Authorized candidates absent from the required inventory SHALL NOT be selected merely because they are executable.

#### Scenario: Required authorized branch may enter existing Tap mechanics
- **WHEN** A is in the proven required inventory, is not complete, and its source candidate is authorized
- **THEN** Agent may nominate at most one existing Tap step for A

#### Scenario: Required but unauthorized branch remains unresolved
- **WHEN** A is required but authorization is false or null
- **THEN** A has zero matching journal and Environment dispatches and the Runtime does not claim the branch, parent, or Goal complete

#### Scenario: Authorized but not required candidate is not selected
- **WHEN** candidate X is authorized but absent from the complete required inventory
- **THEN** Agent does not nominate X solely from authorization

### Requirement: Continue route discovery from fresh reconciled Containers
After one required authorized branch is nominated, existing Container/Traversal mechanics SHALL execute at most one Tap and obtain fresh post-action evidence. Agent SHALL reconcile that evidence before switching the active Container or evaluating another inventory. A valid new semantic Container SHALL be able to establish its own inventory through the same bounded criterion even when its concrete child targets were absent from the initial Plan. Dispatch success, changed visual evidence, or action history SHALL NOT independently prove the child identity or inventory.

#### Scenario: Discover a multi-level route without concrete Plan targets
- **WHEN** P evidence proves A, fresh reconciled A evidence proves C, both candidates are independently authorized, and neither target appears in the initial Plan
- **THEN** Agent nominates exactly one Tap for A and then exactly one Tap for C, with fresh reconciliation between them

### Requirement: Enforce semantic depth independently of viewport movement
Semantic depth SHALL be derived by Agent from accepted parent-to-child Container transitions associated with existing branch-progress evidence. Plan index, action count, Observation sequence, and viewport movement SHALL NOT be semantic depth. If current depth or parent association is ambiguous, route continuation SHALL remain unresolved. The Goal criterion SHALL enforce its captured bounded depth without preventing SC-P3-CAND-007 same-Container exploration.

#### Scenario: Candidate beyond semantic depth is not dispatched
- **WHEN** fresh evidence exposes another child at the approved semantic depth bound
- **THEN** the bounded inventory result does not authorize deeper route continuation and no matching child Tap is dispatched

#### Scenario: Viewport movement does not consume semantic depth
- **WHEN** the same semantic Container accepts additional viewport Observations under SC-P3-CAND-007
- **THEN** its semantic depth remains unchanged while the accepted evidence may contribute to the inventory criterion

### Requirement: Preserve progress and completion boundaries
Accepted inventory refreshes SHALL preserve valid SC-P3-CAND-004 completed-sibling evidence only when it remains a subset of the freshly proven inventory. A parent revisit SHALL NOT blindly redispatch a proven branch. Proven empty inventory, inventory exhaustion, route continuation, action dispatch, Container transition, or local completion SHALL NOT independently set GoalEvidence or `RunState.Completed`. Only Agent consumption of independently satisfied GoalEvidence SHALL complete the Run.

#### Scenario: Parent revisit does not duplicate completed work
- **WHEN** Agent returns to P with valid completion evidence for A and fresh P inventory still contains A and B
- **THEN** A remains proven, is not blindly redispatched, and B remains the unresolved required branch

#### Scenario: Empty leaf requires separate GoalEvidence
- **WHEN** the current inventory is positively empty but GoalEvidence is unsatisfied
- **THEN** the Runtime does not complete the Run from leaf evidence

### Requirement: Replay bounded cross-page discovery deterministically
The Runtime SHALL produce deterministic SC-P3-CAND-008 evidence when RunId, Goal criteria, accepted Observations, initial Plan, Environment transitions, and semantic depth boundary are equal.

#### Scenario: Equal discovery inputs replay equally
- **WHEN** positive, unresolved, authorization-denied, depth-bound, progress-preservation, or empty-leaf branches run twice with equal inputs
- **THEN** inventory evidence/reasons, branch progress, ActionHistory, journal, Trace, GoalEvidence, and final RunState are equal

### Requirement: Preserve the approved production and architecture boundary
SC-P3-CAND-008 SHALL add exactly one immutable production type and exactly three production fields total: two fields on `BranchInventoryEvidence` and one optional immutable Goal criterion field. It SHALL add no enum, interface, component, mutable-state field, mutable-state owner, generic dynamic planner/re-plan, graph/tree/stack, persistent route model, manager, workflow engine, FSM, new Back action, Fingerprint, Confidence, Vision/VLM semantic, generic retry/uncertainty framework, new Recovery behavior, Capstone implementation, Harness change, S1/S2/S3 work, or Runtime refactor. Ownership and authority SHALL remain unchanged.

#### Scenario: Minimum inventory purchase is sufficient
- **WHEN** all SC-P3-CAND-008 branches pass
- **THEN** the capability remains within the approved one-type/three-field budget and all deferred abstractions remain absent
