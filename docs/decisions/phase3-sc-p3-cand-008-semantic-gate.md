# SC-P3-CAND-008 Semantic Gate — Bounded Fresh-Evidence Cross-Page Branch Discovery

> Date: 2026-08-09 | Status: APPROVED | Decision: `SEMANTIC_PURCHASE_REQUIRED`
> Scope: bounded Scenario registration and Semantic Gate only. OpenSpec task generation, Runtime code, Runtime tests, Harness changes, Capstone implementation, Runtime refactor, and S1/S2/S3 work are not authorized by this decision.

## Candidate

- ID: `SC-P3-CAND-008`
- Title: **Bounded Fresh-Evidence Cross-Page Branch Discovery and Route Continuation**
- Evidence confidence: `HIGH`
- Dependencies: SC-P3-CAND-004 branch-progress evidence, SC-P3-CAND-006 candidate authorization, SC-P3-CAND-007 accepted same-Container evidence, and frozen Agent/Container/Traversal/Environment ownership.

## Reality Evidence

Read-only evidence from `feature/agent-runtime` establishes three related failures:

- `MultiBranchNavigationTests` records first-branch-only traversal followed by false `AllVisited`, including a non-scrollable control.
- `SettingsEnumerateRegression` records dynamically discovered descendants crossing the approved semantic depth bound.
- `TraceReplay_20260805T052309367Z_Enumerate` records DFS child revisit until `max_steps` with `settings_home_not_restored`.

The old DynamicMatch, DFS, Frame, FSM, graph/stack, retry, and completion-enum mechanisms are evidence sources only and are not requirements.

## Approved Reality Distinction

```text
candidate observed
!=
candidate authorized
!=
candidate belongs to the complete required branch inventory
!=
candidate selected next
!=
branch completed
```

The Runtime must be able to derive one complete bounded required-branch inventory from fresh accepted evidence for each active semantic Container. It must then nominate at most one unresolved required and independently authorized branch through existing Tap mechanics, obtain fresh evidence, reconcile the resulting Container, and repeat without requiring the complete concrete route in the initial Plan.

## Existing Semantic Audit

- `Observation` and `ObservedElement` prove what was externally observed. They do not prove authorization, required-work membership, inventory completeness, or completion.
- `CandidateAuthorizationEvidence` distinguishes authorized, rejected, and unresolved candidates, but its frozen contract explicitly does not decide required-work membership.
- `BranchProgressEvidence` can preserve an approved inventory and completion evidence, but current production establishes that inventory only by intersecting the initial Observation with exact Tap targets already present in Plan.
- `ViewportExplorationEvidence` and Container-retained accepted Observations can prove whether bounded same-Container evidence collection continues, exhausts, or remains unresolved, but they do not classify a cross-page branch inventory.
- `Plan` is an immutable hypothesis. It may provide initial intent and existing action vocabulary, but it cannot prove the discovered route and must not pre-enumerate every concrete page/action merely to satisfy the Capstone.
- `GoalEvidence` is final completion evidence and cannot be reused as a page-local required-inventory result.
- Traversal can execute one known local step but cannot decide required branch membership or rewrite Plan.

Existing semantics therefore lack a reason-bearing result that says whether the complete required branch inventory for the current bounded evidence is proven, including the valid empty-leaf case.

## Minimum Semantic Purchase

### `BranchInventoryEvidence`

Add exactly one immutable production value with two fields:

1. nullable immutable `RequiredBranchEvidence` map from semantic branch identity to source Observation sequence;
2. non-empty deterministic `Reason`.

Meaning:

- non-null map: the supplied bounded accepted evidence positively proves the complete required branch inventory;
- empty non-null map: the current bounded scope is positively proven to contain no required child branch;
- null map: inventory completeness is unresolved and grants no route continuation or completion;
- map membership is required-work evidence only, not candidate authorization, dispatch, world effect, or completion.

### `Goal.BranchInventoryEvaluator`

Add exactly one optional immutable Goal field with semantic shape:

```csharp
Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>?
    BranchInventoryEvaluator
```

The inputs are bounded accepted same-Container Observation evidence and the current evidence-backed semantic depth. The evaluator must be deterministic, side-effect-free, and depend only on those inputs plus immutable Goal scope captured by the caller. It cannot call Environment, dispatch, mutate Runtime owners, authorize candidates, or set RunState.

An absent evaluator preserves existing fixed-Plan behavior.

## Depth and Evidence Boundary

Semantic depth is an Agent interpretation derived from accepted parent/child Container transitions and existing cross-Container progress evidence. It is not Plan index, action count, viewport count, or a world truth reported by Environment. If the current depth or parent association cannot be proven, route continuation remains unresolved.

The Goal criterion carries the approved scope/depth boundary. At the bound it may positively return an empty inventory; it may not silently reinterpret incomplete evidence as a leaf. Same-Container viewport movement remains governed independently by SC-P3-CAND-007 and is not consumed as semantic depth.

## Formal Scenario Boundary

Positive:

```text
fresh P evidence proves required branch A
→ A is independently authorized
→ Agent nominates one existing Tap for A although A is absent from the initial fixed Plan
→ fresh Observation reconciles to child Container A
→ fresh A evidence proves required branch C
→ C is independently authorized and nominated once
→ fresh Observation reconciles to child Container C
→ fresh C evidence positively proves an empty bounded inventory
→ only independently satisfied GoalEvidence may complete the Run
```

The initial Plan does not contain the concrete P → A → C route.

Negative and composition proof must show:

1. null inventory evidence dispatches no discovered branch and cannot fabricate a leaf, subtree completion, or Run completion;
2. a required branch whose authorization is false or null dispatches zero actions and remains unresolved;
3. stale, incomplete, or conflicting Container evidence cannot replace a valid inventory or attach it to another semantic page;
4. candidates beyond the approved semantic depth are not dispatched, while same-Container viewport exploration remains independent;
5. a parent revisit preserves completed SC-P3-CAND-004 evidence and does not blindly redispatch it;
6. equal RunId, Goal criteria, accepted Observations, initial Plan, and Environment transitions replay equal inventories, progress, actions, journal, Trace, GoalEvidence, and RunState.

## Purchase Budget

- New production model types: **1** (`BranchInventoryEvidence`).
- New production fields: **3** total — two immutable evidence fields plus one optional immutable Goal field.
- New enums: **0**.
- New interfaces: **0**.
- New components: **0**.
- New mutable-state fields: **0**.
- New mutable-state owners: **0**.
- Ownership delta: **NONE**.
- Authority delta: **NONE**.

Existing Agent-owned branch-progress state remains the sole cross-Container progress owner. OpenSpec may choose the smallest immutable map representation but may not expand this budget without another Gate.

## Ownership and Authority

- Agent remains the sole authority for Goal-scoped inventory interpretation, semantic depth, next-branch selection, active Container changes, GoalEvidence, and final RunState.
- Container remains the sole owner of page-local accepted evidence, identity continuity, and local progress.
- Traversal remains the deterministic executor of one nominated local step.
- Environment reports external Observations and dispatch outcomes only.
- Recovery ownership remains frozen and receives no branch-discovery authority.

Duplicate owner: **NO**. Duplicate authority: **NO**. Architecture review required: **NO**.

## Explicitly Not Purchased

- generic dynamic planner or generic dynamic re-plan;
- navigation graph/tree/stack, persistent route model, or Container hierarchy;
- BranchManager, NavigationManager, ProgressManager, workflow engine, or FSM;
- new Back action or generic backtracking policy;
- Fingerprint, Confidence, coordinates, Vision/VLM/AI semantics;
- generic retry or uncertainty framework;
- new Recovery behavior, Runtime refactor, Harness change, Capstone implementation, or S1/S2/S3 work.

## Next Decision

```text
RECONCILE_SPEC
```

OpenSpec reconciliation must preserve this exact semantic budget and formal Scenario boundary.
