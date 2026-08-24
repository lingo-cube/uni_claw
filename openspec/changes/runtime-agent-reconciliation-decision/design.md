# Design: runtime-agent-reconciliation-decision

> HOW to implement the runtime reconciliation & decision capability. See `proposal.md` for motivation
> and `specs/runtime-agent-reconciliation-decision/spec.md` for the behavior contract. This design adds
> an immutable model + a stateless pure function + a ledger method, and reuses the existing DFS engine
> unchanged.

## Context

The codebase's established reconciliation pattern is `Reconcile.FromObservation` (World/) — a stateless
static function `FromObservation(Observation, resolveSemanticPage) → WorldBelief`, documented as "无状态、
无决策 authority" (stateless, no decision authority). The Phase 2 `ExecutionHypothesisLedger.ReviseFromEvidence`
(Planning/) is another: it maps trace evidence to hypothesis lifecycle transitions (boundary → Revised,
return → Replaced, inventory → Confirmed), post-run and trace-derived.

Phase 3 generalizes this into an explicit `RuntimeDecision` model (Continue/Revise/Escalate) and a
stateless `HypothesisReconciler` that classifies (ExecutionHypothesis, WorldBelief, Trace) → RuntimeDecision.
All inputs are Model/ types, so the reconciler adds no new dependency direction. The integration is in
the Planning layer (where the ledger and DirectiveExecution live), additive and non-breaking.

## Goals / Non-Goals

**Goals:**
- Provide an immutable `RuntimeDecision` record + `RuntimeDecisionState` enum (Continue/Revise/Escalate).
- Provide a stateless `HypothesisReconciler.Reconcile` pure function.
- Integrate additively into `ExecutionHypothesisLedger` (Reconcile method + LatestDecision) and
  `DirectiveExecution` (call inside existing ContinueWith, no signature change).
- Deterministic tests: unit (classification), authority (passivity), scenario (3 scenarios).

**Non-Goals:**
- Real-time mid-loop reconciliation (would require modifying the DFS loop). Out of scope; post-run
  trace-derived reconciliation satisfies the proof goal.
- Agent-observable decision state (adding a field to Agent). The decision is observed via the ledger's
  `LatestDecision`, not via an Agent property.
- Wiring the decision into the closed-world path or the `RunStartRequest` wire surface.
- Performing the escalation (Escalate is a record, not an action). The RuntimeAgent does not escalate;
  it records that the situation exceeds its bounded authority.
- Global decision store, persistent decision, navigation knowledge, scenario strings — forbidden.

## Decisions

### Decision 1: `RuntimeDecision` is an immutable record in `Model/`, analogous to `ExecutionHypothesis`
**Choice:** `src/UniClaw.Runtime/Model/RuntimeDecision.cs` — sealed record + `RuntimeDecisionState` enum,
construction-time validation, no methods beyond accessors.
**Rationale:** Matches `Model/`'s role (pure immutable models, no owner) and the existing
`ExecutionHypothesis`/`GoalEvidence`/`TraceEvent` placement. The decision is a passive observable record,
structurally identical in kind to ExecutionHypothesis (Phase 2). No new component with architecture meaning.
**Alternatives considered:** placing it in `Planning/` (rejected — it is a model); making it a union with
hypothesis (rejected — they are distinct concepts; a decision references a hypothesis, it is not one).

### Decision 2: `HypothesisReconciler` is a stateless static pure function in `Planning/`, analogous to `Reconcile.FromObservation`
**Choice:** `src/UniClaw.Runtime/Planning/HypothesisReconciler.cs` — `static` class with
`Reconcile(ExecutionHypothesis, WorldBelief?, IReadOnlyList<TraceEvent>) → RuntimeDecision`. Pure, no state.
**Rationale:** Structurally identical to `Reconcile.FromObservation` (World/) — stateless, no decision
authority, no world observation. The reconciler classifies evidence into a decision state; it does not
perform the decision. Placing it in `Planning/` (sibling to the ledger) keeps the reconciliation close to
the hypothesis lifecycle it reconciles.
**Alternatives considered:** placing it in `World/` (rejected — it reconciles a Planning concept
[hypothesis] against the world, and World/ should not depend on Planning semantics; Model/ types keep it
clean); an instance reconciler (rejected — YAGNI, no state, no test-seam need).

### Decision 3: Escalate is a record, not an action
**Choice:** The Escalate state is a classification of evidence (run failed + authority-boundary reason, or
revised + failed). The RuntimeDecision with Escalate state is a passive record that the caller/UniAgent can
observe. The RuntimeAgent does not perform an escalation action.
**Rationale:** The mission's Frozen Architecture Boundary says "No authority movement is allowed." Escalation
as an action would be new authority (the RuntimeAgent escalating to UniAgent). Escalation as a record
preserves the boundary: the RuntimeAgent records that the situation exceeds its bounded authority, and the
caller decides what to do with that record. This mirrors how `Trap` (a record of a drift condition) works —
the Trap records the condition; the Agent decides the recovery.
**Alternatives considered:** the reconciler calling an escalation callback (rejected — adds a new
authority path); the ledger performing a SystemBack on Escalate (rejected — that's Traversal authority).

### Decision 4: The ledger stores the trace reference and gains a Reconcile method
**Choice:** `ExecutionHypothesisLedger` stores the trace reference (passed to `ReviseFromEvidence`) as a
private field, then `Reconcile(WorldBelief?)` delegates to `HypothesisReconciler.Reconcile(Current, belief,
<stored trace>)`. The ledger gains `LatestDecision` (RuntimeDecision?).
**Rationale:** The ledger is already the run-local hypothesis manager; adding `Reconcile` to it keeps the API
in one place. The trace reference is method-local state (the ledger is method-local, discarded after the
run) — not Runtime state. This avoids the caller needing to pass the trace again.
**Alternatives considered:** the caller calling `HypothesisReconciler.Reconcile` directly with all three
inputs (rejected — redundant trace passing; the ledger already consumed it); storing the full trace in the
ledger (rejected — only the reference is needed; the trace is owned by Agent).

### Decision 5: Integration inside the existing ContinueWith, no signature change
**Choice:** `DirectiveExecution.RunDirectiveAsync` calls `ledger.Reconcile(agent.Belief)` inside the existing
ContinueWith (when ledger is non-null), after `ReviseFromEvidence`. No signature change.
**Rationale:** The ContinueWith already runs post-run when the ledger is non-null. Adding the Reconcile call
there is a one-line additive change with zero regression (null ledger = existing behavior). The caller reads
`ledger.LatestDecision` after awaiting the task.
**Alternatives considered:** a new overload (rejected — duplicates the method); changing the return type
(rejected — breaks Phase 2 tests unnecessarily).

## Risks / Trade-offs

- **[Risk] Reconciler misclassifies Continue vs Revise** → Mitigation: the classification is evidence-driven
  (trace boundary events + belief SemanticPage + hypothesis status); dedicated unit tests assert each
  classification path. The decision is a record, so a misclassification is observable, not destructive.
- **[Risk] Escalate is interpreted as an action** → Mitigation: the spec explicitly states "Escalate is a
  record, not an escalation action"; the authority tests assert the decision model has no dispatch/execute
  method; the design decision is documented.
- **[Risk] Ledger stores trace reference → becomes Runtime state** → Mitigation: the ledger is method-local
  (created in RunDirectiveAsync, discarded when it returns); the trace reference is a private field on a
  method-local object, not an Agent/Container/Traversal/Environment field. The run-local isolation test
  asserts this.
- **[Risk] Concurrent broken `Capabilities/Perception/Semantic/` blocks verification** → Mitigation: isolate
  during verification (quarantine the 4 untracked files, rebuild, test, restore) — same proven approach as
  Phase 2. Report the dependency conflict; do not repair unrelated work.
- **[Trade-off] No real-time reconciliation** → Acceptable: post-run trace-derived reconciliation satisfies
  the proof goal. Real-time would require DFS-loop modification (out of scope).

## Migration Plan

- Additive only; no removal or rename. The ledger gains `Reconcile` + `LatestDecision` (additive).
  `DirectiveExecution` gains one line inside the existing ContinueWith. No existing signature is broken.
- Deploy: build `src/UniClaw.Runtime.sln` (isolated from concurrent broken code); run `dotnet test`.
  Existing suites must pass unchanged. New deterministic tests cover the model, reconciler, authority, and
  scenarios.
- Rollback: delete the two new files and revert the two additive modifications; the Runtime is the prior
  Phase 2 state. No shared mutable state, no contract change.
