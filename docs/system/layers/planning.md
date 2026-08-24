# Planning Layer

## 1. Responsibility

Planning preserves caller-authoritative execution hypotheses and semantic
execution representations. It does not own external-world truth, runtime work
inventory, target selection, action dispatch, progress, or Goal completion.

The two supported representation modes remain distinct:

```text
CLOSED_WORLD_CONCRETE
→ existing Plan

OPEN_WORLD_TYPE_LEVEL
→ TypeLevelTraversalSpecification
```

`Plan` is a hypothesis, not reality. A `TypeLevelTraversalSpecification` is a
scope/category/depth/safety/completion/entry boundary, not a concrete future
route or work inventory.

## 2. Intent Semantic Envelope

`IntentSemanticEnvelope` projects already-authoritative structured caller input
into either:

- `Resolved`, containing a Goal and exactly one truthful execution
  representation; or
- `Insufficient`, containing no executable Goal or representation.

Projection does not parse natural language, invent desired state or authority,
observe the world, or generate a route.

## 3. U2 Open-World Execution Seam

`IntentSemanticEnvelopeExecution.RunOpenWorldAsync` is the sole bounded U2
execution seam. It accepts a resolved open-world envelope, validates that the
supplied specification is exhaustive, navigation-only, and has matching scope
and entry boundaries, then forwards only the already-authoritative primitive
and Model values to Agent.

The seam does not:

- discover inventory;
- select or ground targets;
- construct a Plan or route;
- observe or mutate the world;
- evaluate progress or decide completion.

Agent remains independent of the Planning namespace. Existing
`Agent.RunAsync(Goal, Plan, ...)` remains the closed-world execution boundary.

## 3b. Bounded exploration directive decomposition

`Directive` (`Model/`) is an immutable caller-side expression of a bounded
exploration intent: declared scope, entry, depth, safety, completion, and a
caller-injected strategy-rule set (candidate authorization, branch inventory,
viewport exploration, category classification) plus an optional dispatch policy.
It carries no `Plan`, no coordinates, no `DeviceAction`, and no element index.

`DirectiveDecomposer` is a stateless, caller-configured projection: `Decompose`
maps a `Directive` 1:1 onto the existing open-world execution inputs — a
`TypeLevelTraversalSpecification` plus a type-directed `Goal` evaluator assembly
— or returns an `Insufficient` receipt with no execution inputs when a rule
required by the declared completion requirement is missing. It never observes
the world, selects a UI target, constructs a route, or invents a rule; the
RuntimeAgent keeps sole run-level authority.

`DirectiveExecution.RunDirectiveAsync` is the additive entry that wraps a
resolved decomposition in an open-world `IntentSemanticEnvelope.Resolved` and
forwards it through the existing `IntentExecution.RunOpenWorldAsync` seam. No
new Agent public method is added; the bounded DFS engine is unchanged.

`ExecutionHypothesis` (`Model/`) is an immutable, passive, run-local record of
one execution assumption (RunId, DirectiveReference, Objective, ExpectedTransition,
ExpectedOutcome, Confidence, RevisionReason, CreatedAtObservation, Status). It
carries no Plan, coordinates, DeviceAction, element index, scenario string, or
authorization rule — analogous to `TraceEvent`, it records but never decides.

`ExecutionHypothesisLedger` is a run-local, method-local derivation: `RunDirectiveAsync`
gains an optional `ExecutionHypothesisLedger?` parameter (null = existing Phase 1
behavior, zero regression). When provided, the ledger seeds the initial hypothesis
from the directive's declared boundaries, is activated before the run, and is revised
from accepted pre-terminal evidence (Confirm / Revise /
Replace). It holds no authority and is discarded when the run method returns —
consistent with "Planning owns no mutable Runtime state."

## 3c. Runtime reconciliation decision

`RuntimeDecision` (`Model/`) is an immutable, passive, run-local record of one
reconciliation outcome (RunId, State, HypothesisReference, EvidenceReference,
DecisionReason) with `RuntimeDecisionState` = Continue / Revise / Escalate. It is
analogous to `ExecutionHypothesis` and `TraceEvent` — it records a classification
but carries no Action, authorization, UI element, Goal modification, Traversal
control, or execution authority.

`HypothesisReconciler` is a stateless static pure function
(`Reconcile(ExecutionHypothesis, WorldBelief?, IReadOnlyList<TraceEvent>) →
RuntimeDecision`), structurally identical to `Reconcile.FromObservation` (World/):
无状态、无决策 authority — it classifies evidence, it never observes the world,
never calls an Agent method, and never performs the decision. Classification is
evidence-driven: Continue (hypothesis consistent + belief understood + in-scope
progress, no boundary), Revise (external-boundary observation / hypothesis Revised
/ unknown belief), Escalate (authority-boundary failure — identity safety, depth
cutoff, boundary not handled — or Revised + failed run; Escalate is a RECORD, not
an escalation action). All reasons derive from generic trace reasons + belief
state — no scenario strings.

`ExecutionHypothesisLedger` gains additively: a private trace reference captured by
`ReviseFromEvidence`, a `Reconcile(WorldBelief?) → RuntimeDecision` delegating to
`HypothesisReconciler`, and a `LatestDecision` property. `DirectiveExecution` calls
`ledger.Reconcile(agent.Belief)` inside the existing ContinueWith after
`ReviseFromEvidence` (no signature change); the caller reads `ledger.LatestDecision`
after the run. The ledger and its decision remain method-local — never Runtime
state, never an Agent/Container/Traversal/Environment field. The DFS engine is
unchanged.

## 3d. Decision-driven hypothesis adaptation

`HypothesisAdaptation` (`Model/`) is an immutable, passive, run-local record of one
bounded, decision-driven modification of the execution hypothesis (RunId,
AdaptationType, DecisionReference, PreviousHypothesisReference, AdaptedHypothesis,
AdaptationReason) with `HypothesisAdaptationType` = Keep / Replace / Escalate.
It is analogous to `RuntimeDecision` and `ExecutionHypothesis` — it records a
hypothesis modification but carries no Plan, DeviceAction, Tap instruction, UI
element selection, Goal modification, Traversal control, or execution authority.

`HypothesisAdapter` is a stateless static pure function
(`Adapt(RuntimeDecision, ExecutionHypothesis) → HypothesisAdaptation`),
structurally identical to `HypothesisReconciler.Reconcile`: 无状态、无决策
authority — it maps a decision to a bounded hypothesis update and never performs
the update's execution consequences. Mapping is decision-driven: Keep (Continue →
confirm the current hypothesis), Replace (Revise → a NEW Created hypothesis with a
generic boundary-aware objective; NO SystemBack / DeviceAction / Tap — the
ExternalBoundary capability inside the DFS loop remains solely responsible for
boundary handling), Escalate (Escalate → the current hypothesis Revised with an
escalation reason recording inability; NO recovery / retry / action dispatch). All
reasons derive from the decision reason + generic boundary/authority language — no
scenario strings.

`ExecutionHypothesisLedger` gains additively: an `Adapt() → HypothesisAdaptation`
applying `LatestDecision` via `HypothesisAdapter` — the adapted hypothesis becomes
`Current` and is appended to the immutable history (append-only, never rewritten;
a Replace adaptation first records the superseded current as Replaced, mirroring
`ReviseFromEvidence`'s replacement) — and a `LatestAdaptation` property.
`DirectiveExecution` calls `ledger.Adapt()` inside the existing ContinueWith after
`Reconcile` (one additive line, no signature change); the caller reads
`ledger.LatestAdaptation` after the run. The adaptation stays method-local — never
Runtime state, never an Agent/Container/Traversal/Environment field. The DFS
engine, the FSM, and the Agent's authority are unchanged; the Agent never consults
the adaptation for decisions, authorization, completion, or execution.

## 3e. Start-time Strategy Contract admission

`StrategyDirective` (`Model/`) is an immutable typed bounded request authored by
UniAgent. It declares objective, scope, abstract exploration intent, constraints,
Runtime-verifiable completion criteria, and allowed runtime-local adaptation. It
contains no unresolved prose, route, selector, executable callback, or
`DeviceAction`.

`StrategyContractCompiler` validates the request before a Run exists and resolves
semantic criteria only through composition-provided generic capability bindings.
Unsupported versions, capabilities, criteria, completion semantics, or boundary
combinations fail closed. A successful compilation freezes a `ValidatedStrategy`
and produces a non-action `RuntimeExecutionIntent`; it does not assert completion.

`StrategyExecution.RunAsync` binds one run-scoped `StrategyExecutionReasoningSession`
before forwarding the compiled Goal/specification through the existing
`IntentExecution.RunOpenWorldAsync` seam. Checkpoint reasoning reuses the existing
`RuntimeDecision` and `HypothesisAdaptation` records transactionally; the terminal
result is a read-only reasoning receipt and never a lifecycle or completion command.

The DriverHost exposes this as the distinct start-time
`run.strategy.start` operation. The frozen `run.start` payload is unchanged, and
mid-Run strategy replacement remains outside this layer.

## 3f. Optional pre-terminal reasoning checkpoint

The optional pre-terminal seam is implemented but not graduated. Agent creates an
immutable `PreTerminalReasoningSnapshot` only after accepted evidence,
WorldBelief reconciliation, and DFS progress reconciliation, immediately before
the next action authorization boundary. RuntimeAgent owns the transactional
accepted/proposed reasoning revisions and keeps `ExecutionHypothesis`,
`RuntimeDecision`, and `HypothesisAdaptation` internal to that reasoning layer.

`PreTerminalContinuationProposal` is passive evidence only. It contains no action,
target, route, retry/recovery command, lifecycle command, RunState mutation, or
GoalEvidence/completion authority. Agent validates freshness and correlation,
authorizes compare-and-accept of a still-current revision, and independently
decides whether the same Run continues, fails, or completes. `NotSupported` is
therefore fail-closed with zero dispatch; the proposal never performs that choice.

This seam does not add an outer loop or a new Run and does not move DFS, FSM,
Traversal, GoalEvidence, or lifecycle authority. When no evaluator is configured,
the seam is a strict no-op and the existing OpenWorld path remains unchanged.

## 3g. Strategy execution checkpoint loop

`StrategyExecutionEvidenceView` is an immutable, typed, scenario-neutral projection
attached only to strategy-marked pre-terminal snapshots. `StrategyExecutionReasoningSession`
owns the accepted hypothesis/reasoning history for one Run and stages revisions until
Agent compare-and-accept validation succeeds. Ordinary pre-terminal evaluators remain
unchanged. The session has no action, traversal, lifecycle, recovery, or completion
authority; Agent remains the sole owner of those decisions.

## 4. Ownership and Dependency Boundary

- Planning owns no Run, WorldBelief, DFS progress, action, lifecycle, or terminal
  state. RuntimeAgent may own only its bounded transactional reasoning history
  (accepted/proposed revisions) behind the pre-terminal seam; this exception does
  not grant Planning or RuntimeAgent Agent authority.
- Agent owns semantic inventory/progress interpretation and final RunState.
- Container owns page-local state and accepted local evidence.
- Traversal owns local Select → Check → Execute → fresh Observe → Verify.
- Environment owns external Observation and dispatch outcomes only.

No Planner engine, FSM, route registry, or new state owner is introduced by the
U2 or Strategy Contract seams. The Strategy Contract compiler is a stateless
typed admission/projection component, not a user-level planner.
