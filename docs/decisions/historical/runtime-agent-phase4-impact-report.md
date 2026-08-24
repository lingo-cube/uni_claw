# RuntimeAgent Phase 4 Impact Report — Decision Driven Adaptation Capability

> Phase 4 deliverable for the "RuntimeAgent Phase 4 — Decision Driven Adaptation Capability" mission.
> Authority: READ-ONLY architecture inspection. Working artifact, NOT an Architecture Decision, OpenSpec
> spec, or contract amendment. Frozen baselines (Architecture v1, Protocol v1, Contract I-1..I-14,
> charter) govern and are not redefined here.
> Date: 2026-08-22 | Inspector: Leader (GLM-5.2) | Baseline: Phases 1-3 verified clean

---

## 1. Current Phase 1-3 Flow

```
Directive (Model/)
    ↓ DirectiveDecomposer.Decompose        ← stateless projection (Planning/)
DirectiveDecompositionResult.Resolved(Specification, Goal)
    ↓ DirectiveExecution.RunDirectiveAsync  ← additive entry (Planning/)
    │   ├── (optional) ExecutionHypothesisLedger.Activate()
    ├── IntentSemanticEnvelope.Resolved(OpenWorldTypeLevel)
    │   ↓ IntentExecution.RunOpenWorldAsync  ← unchanged seam (Planning/)
    │   ↓ Agent.RunOpenWorldAsync            ← DFS engine (Agent/, UNCHANGED)
    │       ↓ Evidence-driven DFS: discover → authorize → expand → execute →
    │         verify parent return → update WorldBelief → stop on GoalEvidence
    │       ↓ (ExternalBoundary handled inside the loop: SystemBack + verified return)
    │   ↓ RunState (Completed | Failed) + Agent.Trace + Agent.Belief
    └── (optional, Phase 2) ExecutionHypothesisLedger.ReviseFromEvidence(agent.Trace, result)
        ↓ maps trace inflection points → hypothesis lifecycle (Confirmed / Revised / Replaced)
    └── (optional, Phase 3) ExecutionHypothesisLedger.Reconcile(agent.Belief)
        ↓ HypothesisReconciler.Reconcile(Current, belief, trace) → RuntimeDecision
        ↓ stores RuntimeDecision in LatestDecision
```

Existing passive records (all run-local, all owned by Agent per I-2, all drive no decision):
- `ExecutionHypothesis` (Phase 2) — assumption + lifecycle status (Created/Active/Confirmed/Revised/Replaced).
- `RuntimeDecision` (Phase 3) — reconciliation outcome (Continue/Revise/Escalate) + evidence reference + reason.
- `TraceEvent` — real-time evidence stream of every DFS inflection point.

Existing stateless pure functions (all "无状态、无决策 authority"):
- `Reconcile.FromObservation` (World/) — Observation → WorldBelief.
- `HypothesisReconciler.Reconcile` (Planning/) — (Hypothesis, Belief, Trace) → RuntimeDecision.

---

## 2. Missing Decision-to-Hypothesis Loop

The loop is currently **open**: Decision is produced but never applied back to the hypothesis.

```
Current (open loop):
  Hypothesis → Reality (trace/belief) → Decision  ←  STOPS HERE

Target (closed loop):
  Hypothesis → Reality → Decision → Adapted Hypothesis → (next execution cycle)
```

The `ExecutionHypothesisLedger.ReviseFromEvidence` (Phase 2) already produces `Replaced` hypotheses
from trace evidence, and `Reconcile` (Phase 3) produces a `RuntimeDecision`. But there is no explicit,
decision-driven adaptation step that applies the `RuntimeDecision` to produce an updated
`ExecutionHypothesis` with a recorded adaptation reason. The adaptation is implicit in the ledger's
trace-driven revision; it is not a first-class, decision-driven, observable model.

---

## 3. Proposed HypothesisAdaptation Model

### `HypothesisAdaptationType` (enum, Model/)
```
Keep = 1      // current hypothesis remains valid (Decision: Continue)
Replace = 2   // current hypothesis no longer explains reality (Decision: Revise)
Escalate = 3  // RuntimeAgent cannot adapt inside current authority (Decision: Escalate)
```

### `HypothesisAdaptation` (immutable sealed record, Model/)
Fields:
- `RunId` (string) — run identity.
- `AdaptationType` (HypothesisAdaptationType) — Keep / Replace / Escalate.
- `DecisionReference` (string) — references the RuntimeDecision that drove this adaptation (its reason
  + state), NOT the decision object (keeps the adaptation a passive record).
- `PreviousHypothesisReference` (string) — references the hypothesis before adaptation.
- `AdaptedHypothesis` (ExecutionHypothesis) — the updated hypothesis (a new immutable record;
  Keep = same hypothesis; Replace = new hypothesis with boundary interpretation; Escalate = same
  hypothesis with an escalation-marked status).
- `AdaptationReason` (string) — why this adaptation was applied (derived from the decision reason,
  generic — NO scenario strings).

**Carries NO**: Plan, DeviceAction, Tap instruction, UI element selection, Traversal control, Goal
modification, authorization, or execution authority. It only answers: "Given current evidence, how
should the RuntimeAgent update its local assumption?"

### `HypothesisAdapter` (stateless static, Planning/)
- `Adapt(RuntimeDecision decision, ExecutionHypothesis currentHypothesis) → HypothesisAdaptation`
- Pure function, no state, no authority. Mirrors `HypothesisReconciler.Reconcile` discipline.
- Adaptation logic (decision-driven, generic):
  - **Keep** (Decision: Continue): the adapted hypothesis is the current hypothesis with Status
    Confirmed (if not already). No new assumption. AdaptationReason from the decision reason.
  - **Replace** (Decision: Revise): the current hypothesis is marked Replaced; a new hypothesis is
    created with a boundary-aware objective derived from the decision's evidence reference (generic:
    "External boundary relation requires bounded return handling" — NOT a scenario string, NOT a
    SystemBack instruction). The new hypothesis Status is Created. AdaptationReason from the decision
    reason.
  - **Escalate** (Decision: Escalate): the adapted hypothesis is the current hypothesis with Status
    Revised and an escalation-marked RevisionReason (recording inability). NO recovery action. NO
    automatic retry. AdaptationReason records the authority boundary.

### `ExecutionHypothesisLedger` (extended, Planning/)
- Gains `Adapt() → HypothesisAdaptation` method: reads `LatestDecision` (produced by Phase 3
  `Reconcile`), delegates to `HypothesisAdapter.Adapt(LatestDecision, Current)`, applies the adapted
  hypothesis to `_current` (appending to `_history`), stores the adaptation in `LatestAdaptation`,
  and returns it.
- Gains `LatestAdaptation` property (HypothesisAdaptation? — null until Adapt is called).
- The ledger remains method-local (not Runtime state).

### `DirectiveExecution.RunDirectiveAsync` (additive, no signature change)
- Inside the existing ContinueWith, after `Reconcile` (Phase 3), call `hypothesisLedger.Adapt()`.
  The caller reads `hypothesisLedger.LatestAdaptation` after awaiting.

---

## 4. Authority Analysis

**NONE.**

- `HypothesisAdaptation` is a **passive record** — structurally identical to `ExecutionHypothesis`
  (Phase 2) and `RuntimeDecision` (Phase 3). It has NO methods that authorize, execute, decide
  execution, modify completion, or control Traversal. It only records an adaptation.
- `HypothesisAdapter` is a **stateless pure function** — structurally identical to
  `HypothesisReconciler.Reconcile` (Phase 3) and `Reconcile.FromObservation`. It maps a decision +
  hypothesis → an adaptation; it does not perform the adaptation (the ledger applies the adapted
  hypothesis to its method-local `_current`; the Agent is never consulted and never consults the
  adaptation).
- **Replace does NOT execute SystemBack** — the mission is explicit: "This does not execute
  SystemBack. Existing ExternalBoundary capability remains responsible." The Replace adaptation only
  records a boundary-aware objective in the new hypothesis; the actual boundary handling
  (SystemBack + verified return) is already done inside the DFS loop by
  `TryHandleExternalBoundaryAsync` (Agent.OpenWorld.cs:1023). The adaptation is a post-run
  interpretation, not an execution command.
- **Escalate does NOT recover** — the mission is explicit: "Record inability. Do not automatically
  recover." The Escalate adaptation records the authority boundary in the hypothesis; it does not
  retry, recover, or dispatch anything.
- The Agent's authority is **unchanged**: the Agent does not consult the adaptation for decisions,
  authorization, completion, or execution. The DFS engine is unchanged.
- No new state owner (the ledger is method-local; the adaptation is an immutable record), no new
  decision authority, no new component with architecture meaning.
- Verified against: v1 invariants 2-4, Contract I-2/I-3/I-5/I-12/I-13, the mission's Responsibility
  Split (RuntimeAgent owns hypothesis interpretation; Agent owns authorization/execution; FSM owns
  lifecycle; Traversal owns actions).

---

## 5. FSM Interaction Analysis

The FSM (RunState: Idle → Initializing → Running → Completed | Failed) is owned by Agent (I-2). The
adaptation does NOT interact with the FSM:
- The adaptation is produced **post-run** (after RunState has reached Completed or Failed), inside
  the existing ContinueWith in DirectiveExecution. It does not transition RunState.
- The adaptation does not create, modify, or query RunState. The `HypothesisAdapter` consumes only
  `RuntimeDecision` + `ExecutionHypothesis` (both Model/ types); it never touches RunState.
- The `ExecutionHypothesisStatus` lifecycle (Created/Active/Confirmed/Revised/Replaced) is a
  hypothesis-internal lifecycle, NOT the FSM. It is owned by the ledger (method-local), not by the
  Agent's RunState. The adaptation updates the hypothesis status (e.g. Replaced → new Created), not
  the RunState.

**No FSM responsibility is duplicated.** The FSM owns state transitions; the adaptation owns
hypothesis-record updates. They are distinct.

---

## 6. Agent Interaction Analysis

The Agent owns semantic identity, authorization, grounding, action permission, and execution decision
(I-3). The adaptation does NOT interact with the Agent:
- The adaptation is produced in the Planning layer (DirectiveExecution + ledger + adapter), not in
  the Agent layer. The Agent is never called by the adapter or the ledger's Adapt method.
- The Agent does not consult the adaptation for any decision. The `RuntimeDecision` (Phase 3) is
  already not consulted by the Agent; the adaptation (which wraps the decision) is likewise not
  consulted.
- The adapted hypothesis is observed by the caller (via `ledger.LatestAdaptation`), not by the Agent.
- The DFS engine (`Agent.RunOpenWorldAsync`) is byte-unchanged. The ExternalBoundary handling inside
  the loop is byte-unchanged.

**No Agent authorization boundary is weakened.** The adaptation records; the Agent decides.

---

## 7. Minimal Implementation Point

The adaptation happens **after `Reconcile`** (Phase 3), inside the existing ContinueWith in
`DirectiveExecution.RunDirectiveAsync`:

```
DirectiveExecution.RunDirectiveAsync
    ├── (Phase 2) ledger.ReviseFromEvidence(agent.Trace, result)
    ├── (Phase 3) ledger.Reconcile(agent.Belief) → RuntimeDecision (stored in LatestDecision)
    └── (Phase 4) ledger.Adapt() → HypothesisAdaptation (stored in LatestAdaptation)  [NEW]
        ↓ HypothesisAdapter.Adapt(LatestDecision, Current) → HypothesisAdaptation
        ↓ applies AdaptedHypothesis to _current (appends to _history)
```

This is a **one-line additive call** inside the existing ContinueWith (when the ledger is non-null).
No signature change. No DFS modification. No Agent modification. No FSM modification.

---

## 8. Regression Risk

- **Low** for the non-invasive design: additive immutable model + stateless function + ledger method +
  one line in ContinueWith. The DFS engine, Agent, Container, Traversal, Recovery, World,
  IntentExecution are all unchanged.
- **Low** for regression: the Adapt call is inside the existing optional-ledger ContinueWith (null
  ledger = zero regression, same as Phases 2-3). All Phase 1-3 + existing suites must remain green.
- **None** for authority: the adaptation is passive by construction; authority tests verify this
  structurally (no authorize/execute/bypass/complete/recurse methods; Replace does not execute
  SystemBack; Escalate does not recover).
- **Concurrent work isolation**: the `Capabilities/Perception/Semantic/` tree (untracked, pre-existing)
  now compiles (its owner fixed the CS0411 mid-Phase-3). It still has a pre-existing scroll-guard
  failure ("DeveloperOptions" token in SemanticEvidence.cs) outside Phase 4 scope. If it blocks
  verification, isolate via quarantine-verify-restore (proven Phase 2-3 approach).

---

## 9. Test Plan

### Unit Tests
- `HypothesisAdaptationTests`: construction exposes only adaptation fields; rejects blank
  RunId/AdaptationReason/refs; enum exhaustive (Keep/Replace/Escalate); carries no Action/authorization/
  UI/Goal/Traversal/scenario-string.
- `HypothesisAdapterTests`:
  - Keep (Decision Continue → adapted hypothesis = current with Status Confirmed; no new assumption).
  - Replace (Decision Revise → current marked Replaced; new hypothesis Created with boundary-aware
    objective; NO SystemBack, NO action).
  - Escalate (Decision Escalate → adapted hypothesis = current with Status Revised + escalation reason;
    NO recovery, NO retry).
  - Deterministic (same inputs → structurally identical adaptation). Stateless (no instance/static
    state). No scenario strings in reasons.
- `HypothesisAdaptationRunLocalIsolationTests`: LatestAdaptation per-run; two runs independent; ledger
  not retained in any Agent/Container/Traversal/Environment field.
- `HypothesisAdaptationHistoryTests`: the ledger's History preserves all hypotheses (immutable); an
  adaptation appends a new hypothesis without rewriting prior entries.

### Authority Tests
- `HypothesisAdaptationAuthorityTests`:
  - The adaptation model and adapter expose NO method that authorizes, executes, dispatches, creates a
    container, or initiates a sub-run (no recursive authority).
  - Replace does NOT execute SystemBack or any DeviceAction (assert no DeviceAction/Tap in the
    adaptation or adapter output).
  - Escalate does NOT recover or retry (assert no Recovery/system-back/dispatch in the adaptation).
  - The RunState is produced by the Agent's DFS engine, not by the adaptation (Fake-env end-to-end).
  - The GoalEvidence is evaluated by the existing evaluator, not by the adaptation.
  - The Agent authorization path does not reference the adaptation.

### Scenario Tests (Fake World)
- `AdaptationScenario1KeepTests`: hypothesis "navigate recursive child" + observation expected child
  reached + Decision Continue → Keep adaptation (hypothesis remains active/confirmed).
- `AdaptationScenario2ReplaceTests`: hypothesis "recursive child expected" + observation external
  boundary + Decision Revise → Replace adaptation (new hypothesis records boundary interpretation;
  NO action execution; existing ExternalBoundary capability handled it inside the DFS loop).
- `AdaptationScenario3EscalateTests`: hypothesis "execution possible" + observation authority boundary
  exceeded + Decision Escalate → Escalate adaptation (records inability; NO automatic recovery).

### Regression
- Build 0/0; full suite green (1596+); SETTINGS-TREE-01, U2OpenWorld, OpenWorldTypeDirected, Phase 1
  Directive, Phase 2 Hypothesis, Phase 3 RuntimeDecision, ArchitectureGuard, check-consistency all pass.
- Only pre-existing env-gated RealDevice/RealEmulator + the concurrent scroll-guard may fail.
