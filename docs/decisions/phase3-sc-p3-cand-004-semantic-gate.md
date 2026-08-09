# SC-P3-CAND-004 Semantic Gate — Multi-Page Sibling Branch Progress

> Date: 2026-08-08 | Status: APPROVED | Decision: `SEMANTIC_PURCHASE_REQUIRED`
> Scope: Semantic Gate only. OpenSpec, implementation tasks, Runtime code, Runtime tests, and refactor are not authorized by this decision.

## Reality Evidence

Read-only legacy evidence from `feature/agent-runtime`:

- `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs`
  - `TwoBranch_BothListsVisited`: records the failure mode “list A visited, list B unvisited, yet `AllVisited`”.
  - `DeepNavigation_AllLevelsVisited`: records child completion plus parent-return/deeper-level pressure.
  - `NonScrollableControl_BothBranchesVisited`: proves the skipped-sibling distinction is independent of scrolling.
- `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs`
  - `SettingsApp_FullTraversal_AllVisited`: supplies full Settings traversal integration pressure.
- `tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs`
  - `Enumerate_StopsAtDepth2`: supplies four-level Settings shape and bounded-depth evidence.

Legacy graphs, frames, stacks, `PressBack`, FSM names, and completion enums are evidence mechanisms only and are not requirements.

## Existing Semantic Audit

- `Plan` is an immutable fixed array of `PlanStep`; it is a hypothesis and cannot by itself prove observed branch inventory or completion.
- Agent owns Goal, Plan, WorldBelief, active Container switching, high-level decisions, and final completion, but has no cross-Container branch-progress value.
- Container owns one semantic page and local progress only. `Bind` resets `ExecutedSteps` and `IsLocalComplete`.
- Traversal owns a local append-only execution journal. A journal entry proves dispatch/observation mechanics, not semantic branch or subtree completion.
- Environment reports observations and dispatch outcomes only.
- Recovery owns restore/observe/verify mechanics only and has no higher-level progress authority.
- `GoalEvidence` evaluates an Observation for final Goal completion; its current single-observation surface is not persistent cross-Container progress state.

Current semantics therefore cannot honestly distinguish child-local completion from parent/subtree completion, a parent revisit from newly completed work, or some proven children from all approved siblings proven complete.

## Approved Reality Distinction

The Runtime must represent an immutable, evidence-backed snapshot of progress for one bounded semantic parent scope:

1. the parent semantic identity;
2. fresh evidence for the complete approved sibling inventory within the Scenario boundary;
3. per-branch evidence of proven completion.

Parent/subtree completion is derivable only when fresh inventory evidence exists and every approved sibling has valid completion evidence. A revisit or new Observation cannot by itself add completion, and stale or conflicting identity evidence cannot be attached to another parent scope.

## Minimum Semantic Purchase

Type: one immutable `BranchProgressEvidence` model value; exact collection/storage representation is deferred to OpenSpec.

Owner: Agent.

Meaning: cross-Container evidence for a bounded parent scope, separating known approved sibling inventory from branches whose completion is proven.

Why existing semantics are insufficient: Container state is single-page and reset on bind; Plan is a fixed hypothesis; Traversal journal is local mechanics; GoalEvidence is final-completion evidence rather than persistent progress ownership.

Why evidence purchases it: the legacy receipts reproduce skipped siblings and false `AllVisited`, while SC-S0-CAPSTONE-001 requires honest sibling/subtree completion across several semantic levels.

Why smaller is insufficient: a boolean, child-local completion flag, visited-page list, Plan index, or journal replay cannot jointly prove sibling inventory, parent association, and evidence-backed per-branch completion.

Approved budget:

- New production model types: 1.
- New production fields: 4 total — three immutable semantic fields on the evidence value plus one Agent-owned state field holding immutable snapshots.
- New enums: 0.
- New interfaces: 0.
- New components: 0.
- New mutable state owners: 0; Agent remains the sole owner.
- Ownership delta: NONE.
- Authority delta: NONE.

The three semantic fields are parent semantic identity, approved sibling-inventory evidence, and proven branch-completion evidence. OpenSpec may choose the smallest immutable representation but may not expand this budget without a new Gate.

## Formal Scenario Boundary

Positive:

```text
fresh P evidence proves approved children A and B
→ enter A
→ prove A complete
→ return through an existing approved visible affordance to P
→ P/subtree completion remains forbidden because B lacks proof
→ enter B
→ prove B complete
→ return/reconcile with P
→ only now may higher-level evidence support bounded subtree completion
```

Negative proof must show:

1. A complete with B unvisited cannot complete P/subtree.
2. Returning to P preserves valid A evidence without fabricating B evidence.
3. Revisiting A does not create a second distinct completion.
4. Stale or absent evidence cannot prove inventory or branch completion.
5. Conflicting parent/child semantic identity cannot receive another scope's progress.
6. Child-local completion cannot directly trigger final Agent Goal completion.

Backtracking remains execution mechanics within this Scenario and may use existing approved visible affordances plus existing actions. No new Back semantic is purchased.

## Deferred Boundaries

- Recovery-progress validity after external drift: `SEPARATE_RESEARCH_REQUIRED`. The progress value can remain in Agent state, but verified world recovery does not prove that pre-drift branch evidence is still valid.
- Autonomous discovered-candidate safety: `RESEARCH`.
- Navigation graph/tree/stack, visited-set semantic type, TraversalContext, ResumeToken, managers, FSM, generic workflow engine, Container hierarchy, and new Recovery semantics: not purchased.
- Agent refactor: deferred. Ownership and authority are unambiguous; current structural pressure remains non-blocking.

## Next Decision

```text
RECONCILE_SPEC
```

OpenSpec reconciliation must preserve this exact semantic budget and formal Scenario boundary.
