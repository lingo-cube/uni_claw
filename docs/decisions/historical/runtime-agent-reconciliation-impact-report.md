# RuntimeAgent Reconciliation Impact Report (Phase 3)

> Phase 3 deliverable for the "RuntimeAgent Phase 3 — Runtime Reconciliation & Decision Capability" mission.
> Authority: READ-ONLY architecture inspection. Working artifact, NOT an Architecture Decision, OpenSpec
> spec, or contract amendment. Frozen baselines (Architecture v1, Protocol v1, Contract I-1..I-14,
> charter) govern and are not redefined here.
> Date: 2026-08-21 | Inspector: Leader (GLM-5.2) | Baseline: Phase 1 + Phase 2 verified clean

---

## 1. Current Execution Flow

After Phases 1-2, the exploration pipeline is:

```
Directive (Model/)
    ↓ DirectiveDecomposer.Decompose        ← stateless projection (Planning/)
DirectiveDecompositionResult.Resolved(Specification, Goal)
    ↓ DirectiveExecution.RunDirectiveAsync  ← additive entry (Planning/)
    │   ├── (optional) ExecutionHypothesisLedger.Activate()
    ├── IntentSemanticEnvelope.Resolved(OpenWorldTypeLevel)
    │   ↓ IntentExecution.RunOpenWorldAsync  ← unchanged seam (Planning/)
    │   ↓ Agent.RunOpenWorldAsync            ← DFS engine (Agent/, UNCHANGED)
    │       ↓ Evidence-driven DFS: discover → authorize → expand → execute → verify → update belief
    │   ↓ RunState (Completed | Failed) + Agent.Trace + Agent.Belief
    └── (optional) ExecutionHypothesisLedger.ReviseFromEvidence(agent.Trace, result)
        ↓ maps trace inflection points → hypothesis lifecycle (Confirmed / Revised / Replaced)
```

Existing run-local observable records (all passive, owned by Agent per I-2):
- `Agent.Trace` (TraceEvent list) — real-time evidence stream of every inflection point.
- `Agent.Belief` (WorldBelief) — current best judgment of reality (revised by Observation).
- `ExecutionHypothesisLedger.Current` (ExecutionHypothesis) — current execution assumption + lifecycle.
- `ExecutionHypothesisLedger.History` — immutable hypothesis sequence.

Existing reconciliation patterns (all stateless, no decision authority):
- `Reconcile.FromObservation` (World/) — Observation → WorldBelief. "无状态、无决策 authority."
- `BindingReconciler.Reconcile` (World/) — binding evidence → binding proposals.
- `StateBeliefReducer` (World/) — observation + bindings → state-belief proposal.
- `ExecutionHypothesisLedger.ReviseFromEvidence` (Planning/) — trace + outcome → hypothesis lifecycle.

---

## 2. Existing Capabilities

- **Directive → decomposition → DFS execution** (Phase 1): bounded directive enters Runtime, feeds the proven DFS engine.
- **Execution hypothesis** (Phase 2): run-local, passive, revisable record of the current execution assumption + lifecycle. Revised from trace evidence post-run. No authority.
- **WorldBelief**: runtime understanding of current world (SemanticPage, Confidence, Evidence, SourceObservationSequence).
- **Trace evidence**: real-time stream of every DFS inflection point (boundary, return, inventory, completion).
- **GoalEvidence + RunState**: completion proof (I-10) + run lifecycle (owned by Agent, I-2).
- **Recovery** (Agent.Recovery.cs): closed-world drift→trap→recover→resume. Open-world DFS fails closed.

---

## 3. Missing Capability

RuntimeAgent can accept a directive, maintain a hypothesis, observe the outcome, and revise the
hypothesis record. But it cannot **explicitly reconcile** the hypothesis against the observed world and
produce a bounded **RuntimeDecision** answering: "Is my current execution hypothesis still consistent
with the observed world?" and "What bounded execution direction should continue?"

The Phase 2 ledger's `ReviseFromEvidence` maps trace → hypothesis lifecycle, but it does not produce an
explicit **decision** (Continue / Revise / Escalate) that classifies the reconciliation outcome. The
decision is implicit in the hypothesis status; it is not a first-class, observable, lifecycle-tracked
model.

Current:
```
ExecutionHypothesis → (revise from trace) → revised hypothesis status (implicit decision)
```

Target:
```
ExecutionHypothesis + WorldBelief + Trace Evidence → Reconciliation → RuntimeDecision (explicit)
```

---

## 4. Minimal Extension Point

The reconciliation is a **stateless pure function** over Model/ types, structurally analogous to
`Reconcile.FromObservation`:

```
HypothesisReconciler.Reconcile(
    ExecutionHypothesis hypothesis,    ← Model/
    WorldBelief? belief,               ← Model/ (Agent.Belief after run)
    IReadOnlyList<TraceEvent> trace)   ← Model/ (Agent.Trace after run)
    ↓
    RuntimeDecision                     ← Model/ (new, passive record)
```

Integration (additive, non-breaking):
```
DirectiveExecution.RunDirectiveAsync
    ├── (Phase 2) ledger.ReviseFromEvidence(agent.Trace, result)
    └── (Phase 3) ledger.Reconcile(agent.Belief) → RuntimeDecision  [new, optional]
        ↓ exposes ledger.LatestDecision for observability
```

The `ExecutionHypothesisLedger` gains:
- A private trace reference (stored when `ReviseFromEvidence` is called — method-local, not Runtime state).
- A `Reconcile(WorldBelief? belief) → RuntimeDecision` method that delegates to the stateless
  `HypothesisReconciler.Reconcile(Current, belief, <stored trace>)`.
- A `LatestDecision` property (the most recent RuntimeDecision, for test observability).

The `DirectiveExecution` integration calls `ledger.Reconcile(agent.Belief)` after `ReviseFromEvidence`
(inside the existing ContinueWith, only when the ledger is non-null). **No signature change** — the
ledger is already an optional parameter; the caller reads `ledger.LatestDecision` after the run.

**The DFS engine, Agent, Container, Traversal, Recovery, World, IntentExecution are UNCHANGED.**

---

## 5. Required Models

### `RuntimeDecisionState` (enum, Model/)
```
Continue = 1     // hypothesis remains consistent with observed world
Revise   = 2     // hypothesis no longer matches world evidence
Escalate = 3     // problem exceeds current RuntimeAgent authority
```

### `RuntimeDecision` (immutable sealed record, Model/)
Fields:
- `RunId` (string) — run identity.
- `State` (RuntimeDecisionState) — Continue / Revise / Escalate.
- `HypothesisReference` (string) — references the reconciled hypothesis (objective + status), NOT the
  hypothesis object (keeps the decision a passive record, not a live reference).
- `EvidenceReference` (string) — summarizes the evidence basis (belief SemanticPage + key trace
  inflection points). An evidence reference, not a truth claim.
- `DecisionReason` (string) — why this decision was reached (derived from trace reasons + belief
  state, generic — NO scenario strings).

**Carries NO**: Action, authorization, UI element selection, Goal modification, Traversal control,
scenario strings, or execution authority. It is a passive record — "given current runtime evidence,
what execution interpretation should continue?"

### `HypothesisReconciler` (stateless static, Planning/)
- `Reconcile(ExecutionHypothesis hypothesis, WorldBelief? belief, IReadOnlyList<TraceEvent> trace)
  → RuntimeDecision`
- Pure function, no state, no authority. Mirrors `Reconcile.FromObservation` discipline.
- Classification logic (evidence-driven, generic):
  - **Continue**: hypothesis Status is Confirmed or Active (not contradicted); belief.SemanticPage is
    non-null (world is understood); trace shows in-scope progress (inventory complete / verified return)
    without boundary contradiction.
  - **Revise**: trace shows EXTERNAL_BOUNDARY_OBSERVED (hypothesis contradicted), OR hypothesis Status
    is Revised, OR belief.SemanticPage is null/unknown (world not understood) but the run is still
    within RuntimeAgent authority (not a terminal authority-boundary failure).
  - **Escalate**: RunState.Failed with an authority-boundary failure reason (e.g. "identity safety",
    "depth cutoff", "boundary not handled"), OR hypothesis Revised + run Failed (RuntimeAgent could not
    reconcile and continue within its bounded authority). Escalate is a RECORD of the authority
    boundary being exceeded — the RuntimeAgent does not perform the escalation (that would be new
    authority); it records that the situation exceeds its bounded scope.

### `ExecutionHypothesisLedger` (extended, Planning/)
- Stores the trace reference when `ReviseFromEvidence` is called (private field, method-local).
- Gains `Reconcile(WorldBelief? belief) → RuntimeDecision` — delegates to
  `HypothesisReconciler.Reconcile(Current, belief, <stored trace>)`.
- Gains `LatestDecision` property (RuntimeDecision? — null until Reconcile is called).

### `DirectiveExecution.RunDirectiveAsync` (additive modification, Planning/)
- Inside the existing ContinueWith (when ledger is non-null): after `ReviseFromEvidence`, call
  `ledger.Reconcile(agent.Belief)`. No signature change.

---

## 6. Authority Impact

**NONE.**

- `RuntimeDecision` is a **passive record** — structurally identical to `ExecutionHypothesis` (Phase 2)
  and `TraceEvent`. It has NO methods that authorize, execute, decide execution, modify completion, or
  control Traversal. It only records a decision state + evidence reference + reason.
- `HypothesisReconciler` is a **stateless pure function** — structurally identical to
  `Reconcile.FromObservation`. "无状态、无决策 authority." It classifies evidence into a decision
  state; it does not perform the decision.
- The **Escalate** state is a RECORD of the authority boundary being exceeded, not an escalation
  action. The RuntimeAgent does not escalate (that would be new authority); it records that the
  situation exceeds its bounded scope, for the caller/UniAgent to observe.
- The Agent's authority is **unchanged**: the Agent does not consult the RuntimeDecision for decisions,
  authorization, completion, or execution. The DFS engine is unchanged.
- No new state owner (the ledger is method-local; the decision is an immutable record), no new decision
  authority, no new component with architecture meaning.
- Verified against: v1 invariants 2-4, Contract I-2/I-3/I-5/I-12/I-13, the "no authority movement"
  rule in the mission's Frozen Architecture Boundary.

---

## 7. Architecture Risk

- **Low** for the non-invasive design: additive immutable model + stateless function + ledger method.
  The DFS engine is untouched. Mechanically guarded by ArchitectureGuardTests + check-consistency.sh.
- **Low** for regression: the Reconcile call is inside the existing optional-ledger ContinueWith (null
  ledger = zero regression, same as Phase 2). All Phase 1-2 + existing suites must remain green.
- **None** for authority: the RuntimeDecision is passive by construction; authority tests verify this
  structurally (no authorize/execute/bypass/complete/recurse methods).
- **Concurrent work isolation**: the broken `Capabilities/Perception/Semantic/` files (CS0411, untracked,
  pre-existing) will be isolated during verification (quarantine + restore, as in Phase 2). They are
  outside Phase 3 scope.

---

## 8. Implementation Plan

1. **Model**: `Model/RuntimeDecision.cs` — `RuntimeDecisionState` enum (Continue/Revise/Escalate) +
   `RuntimeDecision` immutable record with construction validation.
2. **Reconciler**: `Planning/HypothesisReconciler.cs` — stateless static `Reconcile(hypothesis, belief,
   trace) → RuntimeDecision`. Evidence-driven classification (Continue/Revise/Escalate). Generic
   reasons from trace/belief — NO scenario strings.
3. **Ledger extension**: modify `Planning/ExecutionHypothesisLedger.cs` additively — store trace
   reference in `ReviseFromEvidence`; add `Reconcile(WorldBelief?) → RuntimeDecision` delegating to
   `HypothesisReconciler`; add `LatestDecision` property.
4. **DirectiveExecution integration**: modify `Planning/DirectiveExecution.cs` additively — inside the
   existing ContinueWith, call `ledger.Reconcile(agent.Belief)` after `ReviseFromEvidence`. No
   signature change.
5. **Unit tests**: RuntimeDecision creation/validation; HypothesisReconciler Continue (hypothesis
   confirmed + belief understood + in-scope trace); Revise (boundary observed / hypothesis revised /
   belief unknown); Escalate (failed + authority-boundary reason); run-local isolation.
6. **Authority tests**: RuntimeDecision cannot authorize/execute/bypass Agent/alter completion/create
   recursive authority (structural + end-to-end Fake-env).
7. **Scenario tests**: Scenario 1 (child transition expected + child reached → Continue); Scenario 2
   (recursive child expected + external boundary → Revise); Scenario 3 (execution possible + authority
   boundary exceeded → Escalate).
8. **Regression**: build 0/0 (isolated from concurrent broken code); full suite green (1537+);
   SETTINGS-TREE-01, U2OpenWorld, OpenWorldTypeDirected, Phase 1 directive, Phase 2 hypothesis,
   ArchitectureGuard, check-consistency all pass; openspec validate --strict.
