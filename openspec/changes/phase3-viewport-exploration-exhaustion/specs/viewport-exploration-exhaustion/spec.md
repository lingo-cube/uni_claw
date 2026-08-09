## ADDED Requirements

### Requirement: Represent three-valued viewport exploration evidence
The Runtime SHALL add exactly one immutable `ViewportExplorationEvidence` production value with `bool? ContinueExploration` and non-empty `string Reason` fields. `true` SHALL mean bounded same-Container evidence positively justifies one additional forward movement, `false` SHALL mean the evidence positively proves forward semantic exploration exhaustion, and `null` SHALL mean neither conclusion is proven. The value SHALL NOT represent a stable viewport, dispatch result, local completion, branch completion, GoalEvidence, or Run completion.

#### Scenario: Continue exhausted and unresolved remain distinct
- **WHEN** bounded same-Container evidence positively supports another movement, positively proves exhaustion, or proves neither
- **THEN** the value represents the outcomes as `true`, `false`, or `null` respectively with a non-empty deterministic reason

### Requirement: Carry one optional exploration criterion on Goal
The Runtime SHALL add exactly one optional immutable `Goal.ViewportExplorationEvaluator` field with semantic shape `Func<ImmutableArray<Observation>, ViewportExplorationEvidence>?`. The evaluator SHALL be deterministic and side-effect-free, SHALL consume only the supplied bounded same-Container Observation evidence plus immutable Goal scope, and SHALL NOT call Environment, dispatch actions, mutate Runtime owners, or set RunState.

#### Scenario: Existing fixed Plan behavior remains compatible
- **WHEN** Goal has no viewport exploration evaluator
- **THEN** existing fixed-Plan behavior remains unchanged and no repeated-exploration decision is fabricated

#### Scenario: Criterion receives bounded immutable evidence
- **WHEN** Agent evaluates whether viewport exploration should continue
- **THEN** the evaluator receives an immutable bounded same-Container evidence sequence whose final Observation is current and fresh

### Requirement: Retain cross-viewport evidence in Container
Container SHALL be the sole mutable owner of one bounded sequence of accepted same-Container Observations. `Bind` SHALL start the sequence from its bound Observation. A post-movement Observation SHALL be appended only after SC-P3-003 freshness and semantic continuity are proven. Rejected, stale, identity-conflicting, or otherwise unaccepted evidence SHALL NOT be appended. The retained sequence SHALL NOT use Observation sequence, element index, element text, or snapshot equality as stable content identity.

#### Scenario: Accepted viewport evidence accumulates locally
- **WHEN** V1 is bound and fresh V2 and V3 each pass existing viewport continuity verification
- **THEN** the same Container exposes bounded immutable evidence ordered V1, V2, V3 without adding another mutable owner

#### Scenario: Contradictory evidence is not retained as continuity
- **WHEN** post-movement evidence is stale or fails existing same-Container continuity
- **THEN** Container preserves previously accepted evidence, does not append the contradictory Observation, and follows the existing SC-P3-003 escalation boundary

### Requirement: Authorize at most one movement per positive decision
Agent SHALL remain the sole authority for interpreting the exploration criterion. A `true` result SHALL authorize at most the next already-approved `ScrollForward` Plan step. After that movement, Traversal SHALL obtain fresh evidence and existing SC-P3-003 continuity SHALL be verified before any additional movement decision. Agent SHALL NOT blindly repeat `ScrollForward`.

#### Scenario: New relevant evidence justifies one further movement
- **WHEN** fresh accepted evidence introduces Goal-relevant evidence and the bounded criterion returns `true`
- **THEN** Agent may authorize exactly one next approved viewport movement and must decide again from its fresh result

### Requirement: Require positive evidence for semantic exhaustion
Agent SHALL stop requesting viewport movement when the bounded criterion returns `false`. Positive exhaustion SHALL require fresh accepted same-Container evidence plus an explicit bounded criterion proving no further Goal-relevant forward exploration remains. One unchanged element set, rejected or timed-out dispatch, consumed movement budget, no currently authorized candidate, unchanged Fingerprint, or absence of new text SHALL NOT independently prove exhaustion.

#### Scenario: Explicit end evidence proves bounded exhaustion
- **WHEN** fresh accepted evidence contains a positive end/boundary indication and the deterministic criterion returns `false` with a reason
- **THEN** Agent requests no further viewport movement and records positive exhaustion evidence

#### Scenario: Same visible evidence alone is insufficient
- **WHEN** fresh accepted evidence repeats the visible element set but contains no independent positive exhaustion proof
- **THEN** the criterion returns `null`, not `false`, and the Runtime does not claim semantic exhaustion

#### Scenario: Rejected movement is not semantic exhaustion
- **WHEN** Environment rejects a `ScrollForward`
- **THEN** existing dispatch/verification failure behavior applies and the Runtime does not record positive exploration exhaustion

### Requirement: Preserve unresolved evidence and boundedness honestly
When the criterion returns `null`, Agent SHALL perform no further viewport movement for the bounded exploration, SHALL NOT fabricate local or Goal completion, and SHALL stop or escalate using existing authority. The maximum exploration bound SHALL be the finite set of approved viewport Plan steps. If that bound is consumed while the latest evidence still returns `true`, the Runtime SHALL report unresolved/incomplete exploration rather than semantic exhaustion.

#### Scenario: Ambiguous fresh evidence stops safely
- **WHEN** fresh accepted evidence proves neither continuation nor exhaustion
- **THEN** Agent records unresolved evidence, performs no blind additional movement, and does not complete the Container or Run from that outcome

#### Scenario: Movement bound is not content exhaustion
- **WHEN** all approved viewport Plan steps have been consumed while fresh evidence still positively indicates further exploration
- **THEN** the Runtime reports bounded exploration as unresolved/incomplete and does not claim exhaustion or Goal completion

### Requirement: Keep viewport exhaustion separate from Goal completion
Viewport continuation, positive exhaustion, unresolved evidence, movement dispatch, and Container continuity SHALL NOT independently set `Container.IsLocalComplete`, branch completion, GoalEvidence, or `RunState.Completed`. Only Agent consumption of independently satisfied GoalEvidence SHALL complete the Run. An unresolved required exploration branch SHALL NOT be silently ignored for completion.

#### Scenario: Positive exhaustion requires separate GoalEvidence
- **WHEN** bounded exploration is positively exhausted but GoalEvidence is unsatisfied
- **THEN** no further viewport movement occurs and the Run does not complete

#### Scenario: GoalEvidence remains final authority
- **WHEN** bounded exploration is positively exhausted and fresh GoalEvidence independently proves the Goal
- **THEN** Agent may complete the Run because of GoalEvidence, not because of the exhaustion outcome

### Requirement: Replay repeated exploration deterministically
The Runtime SHALL produce deterministic SC-P3-CAND-007 evidence when RunId, Goal evaluators, Plan, initial Observation, Environment transitions, and exploration bound are equal.

#### Scenario: Equal repeated-exploration inputs replay equally
- **WHEN** continue, exhausted, unresolved, or bound-reached branches run twice with equal inputs
- **THEN** retained Observation evidence, exploration outcomes/reasons, ActionHistory, journal, Trace, GoalEvidence, and final RunState are equal

### Requirement: Preserve the approved production and architecture boundary
SC-P3-CAND-007 SHALL add exactly one immutable production type and at most four production fields total: two fields on `ViewportExplorationEvidence`, one optional immutable Goal criterion field, and one Container-owned retained-evidence field. It SHALL add no enum, interface, component, mutable-state owner, stable viewport identity, hierarchy, graph, stack, manager, Fingerprint authority, generic scroll/retry/uncertainty framework, dynamic planner, multi-Container exploration state, Recovery semantic, FSM, Capstone implementation, Harness change, S1/S2/S3 work, or Runtime refactor. Ownership and authority SHALL remain unchanged.

#### Scenario: Minimum evidence purchase is sufficient
- **WHEN** all SC-P3-CAND-007 branches pass
- **THEN** the capability remains within the approved one-type/four-field budget and all deferred abstractions remain absent
