# Design: runtime-agent-decision-adaptation

> HOW to implement the decision-driven hypothesis adaptation. See `proposal.md` for motivation and
> `specs/runtime-agent-decision-adaptation/spec.md` for the behavior contract. This design adds an
> immutable model + a stateless pure function + a ledger method + one line in ContinueWith, and reuses
> the existing DFS engine, FSM, and ExternalBoundary capability unchanged.

## Context

The codebase's established pattern for passive, stateless derivations: `Reconcile.FromObservation`
(World/) and `HypothesisReconciler.Reconcile` (Planning/, Phase 3) — both "无状态、无决策 authority."
Phase 4 adds `HypothesisAdapter.Adapt` (Planning/) following the same discipline, mapping a
`RuntimeDecision` (Phase 3) + `ExecutionHypothesis` (Phase 2) → `HypothesisAdaptation`.

The `ExecutionHypothesisStatus` lifecycle (Phase 2) already includes `Replaced` ("A revised hypothesis
was superseded by a new hypothesis for the next execution phase") — designed for exactly this. The
ledger's `ReviseFromEvidence` (Phase 2) already produces `Replaced` hypotheses from trace evidence;
Phase 4 makes the adaptation explicit and decision-driven.

The ExternalBoundary capability (`TryHandleExternalBoundaryAsync`, Agent.OpenWorld.cs:1023) already
handles boundaries inside the DFS loop (SystemBack + verified return). Phase 4's Replace adaptation
does NOT duplicate this — it only records a boundary-aware objective in the updated hypothesis; the
actual boundary handling is already done and remains solely in the DFS loop.

## Goals / Non-Goals

**Goals:**
- Provide an immutable `HypothesisAdaptation` record + `HypothesisAdaptationType` enum (Keep/Replace/
  Escalate).
- Provide a stateless `HypothesisAdapter.Adapt` pure function.
- Integrate additively into `ExecutionHypothesisLedger` (Adapt method + LatestAdaptation) and
  `DirectiveExecution` (one line inside existing ContinueWith, no signature change).
- Deterministic tests: unit (Keep/Replace/Escalate, isolation, history), authority (passivity), scenario
  (3 scenarios).

**Non-Goals:**
- Executing the adaptation's consequences (SystemBack, recovery, retry). Replace records a boundary-aware
  objective; the DFS loop already handled the boundary. Escalate records inability; no recovery.
- Real-time mid-loop adaptation (would require modifying the DFS loop). Out of scope; post-run
  decision-driven adaptation satisfies the proof goal.
- Agent-observable adaptation state (adding a field to Agent). The adaptation is observed via the ledger's
  `LatestAdaptation`, not via an Agent property.
- Autonomous planner, recovery executor, action selector, authorization layer, global memory — forbidden
  by the mission and the frozen invariants.

## Decisions

### Decision 1: `HypothesisAdaptation` is an immutable record in `Model/`, analogous to `RuntimeDecision`
**Choice:** `src/UniClaw.Runtime/Model/HypothesisAdaptation.cs` — sealed record + `HypothesisAdaptationType`
enum, construction-time validation, no methods beyond accessors.
**Rationale:** Matches `Model/`'s role (pure immutable models, no owner) and the existing
`RuntimeDecision`/`ExecutionHypothesis`/`TraceEvent` placement. The adaptation is a passive observable
record, structurally identical in kind to RuntimeDecision (Phase 3). No new component with architecture
meaning.
**Alternatives considered:** placing it in `Planning/` (rejected — it is a model); making the adapted
hypothesis a separate field outside the record (rejected — the adaptation is a unit; the adapted
hypothesis is part of it).

### Decision 2: `HypothesisAdapter` is a stateless static pure function in `Planning/`, analogous to `HypothesisReconciler`
**Choice:** `src/UniClaw.Runtime/Planning/HypothesisAdapter.cs` — `static` class with
`Adapt(RuntimeDecision, ExecutionHypothesis) → HypothesisAdaptation`. Pure, no state.
**Rationale:** Structurally identical to `HypothesisReconciler.Reconcile` (Phase 3) — stateless, no
decision authority, no world observation. The adapter maps a decision to a bounded hypothesis update; it
does not perform the update's execution consequences. Placing it in `Planning/` (sibling to the
reconciler and ledger) keeps the adaptation close to the decision it applies.
**Alternatives considered:** placing it in `World/` (rejected — it adapts a Planning concept, not the
world); an instance adapter (rejected — YAGNI, no state).

### Decision 3: Replace does NOT execute SystemBack; Escalate does NOT recover
**Choice:** The Replace adaptation records a boundary-aware objective in the new hypothesis (generic:
"External boundary relation requires bounded return handling"). The Escalate adaptation records the
authority boundary in the hypothesis status (Revised + escalation reason). Neither dispatches any action.
**Rationale:** The mission is explicit: "This does not execute SystemBack. Existing ExternalBoundary
capability remains responsible" and "Record inability. Do not automatically recover." The ExternalBoundary
capability (`TryHandleExternalBoundaryAsync`) already handles boundaries inside the DFS loop. Duplicating
that would be new execution authority (forbidden). Escalation as an action would be new authority
(forbidden). Both are records, not actions — preserving the frozen "no authority movement" boundary.
**Alternatives considered:** the adapter calling `DeviceAction.SystemBack` on Replace (rejected — that's
Traversal authority, and it's already done inside the loop); the ledger retrying on Escalate (rejected —
that's new recovery authority).

### Decision 4: The ledger gains Adapt() + LatestAdaptation; integration is one line in ContinueWith
**Choice:** `ExecutionHypothesisLedger.Adapt()` reads `LatestDecision` (Phase 3), delegates to
`HypothesisAdapter.Adapt(LatestDecision, Current)`, applies the `AdaptedHypothesis` to `_current`
(appending to `_history`), stores the adaptation in `_latestAdaptation`. `DirectiveExecution` calls
`ledger.Adapt()` inside the existing ContinueWith after `Reconcile` — one additive line, no signature
change.
**Rationale:** The ContinueWith already runs post-run when the ledger is non-null (Phases 2-3). Adding
the Adapt call there is a one-line additive change with zero regression (null ledger = existing
behavior). The caller reads `ledger.LatestAdaptation` after awaiting.
**Alternatives considered:** a new overload (rejected — duplicates the method); changing the return type
(rejected — breaks Phase 3 tests unnecessarily).

### Decision 5: Adapted hypothesis objective is generic, derived from the decision's evidence reference
**Choice:** The Replace adaptation's new hypothesis objective is "External boundary relation requires
bounded return handling" (generic). The Keep adaptation confirms the current objective. The Escalate
adaptation keeps the current objective with an escalation-marked revision reason. No scenario strings.
**Rationale:** Respects "RuntimeAgent MUST NOT contain application-specific knowledge." The objective is
derived from the decision's evidence reference (which is already generic per Phase 3).
**Alternatives considered:** caller-injected adaptation objectives (rejected — adds caller surface
unnecessarily; the decision already carries the evidence basis).

## Risks / Trade-offs

- **[Risk] Replace is misinterpreted as executing SystemBack** → Mitigation: the spec explicitly states
  "Replace does not execute SystemBack"; the authority tests assert no DeviceAction/Tap/SystemBack in the
  adaptation or adapter output; the design decision is documented.
- **[Risk] Escalate is misinterpreted as recovering** → Mitigation: the spec explicitly states "Escalate
  does not recover"; the authority tests assert no recovery/retry/dispatch; the design decision is
  documented.
- **[Risk] Adaptation modifies history destructively** → Mitigation: the ledger's `_history` is an
  `ImmutableList<ExecutionHypothesis>`; `Adapt()` appends, never rewrites. The history-preservation test
  asserts this.
- **[Risk] Concurrent broken `Capabilities/Perception/Semantic/` blocks verification** → Mitigation: the
  tree now compiles (owner fixed it mid-Phase-3); only a pre-existing scroll-guard failure remains
  (outside Phase 4 scope). Isolate via quarantine-verify-restore if needed; report the conflict.
- **[Trade-off] No real-time adaptation** → Acceptable: post-run decision-driven adaptation satisfies the
  proof goal. Real-time would require DFS-loop modification (out of scope).

## Migration Plan

- Additive only; no removal or rename. The ledger gains `Adapt()` + `LatestAdaptation` (additive).
  `DirectiveExecution` gains one line inside the existing ContinueWith. No existing signature is broken.
- Deploy: build `src/UniClaw.Runtime.sln`; run `dotnet test`. Existing suites must pass unchanged. New
  deterministic tests cover the model, adapter, authority, and scenarios.
- Rollback: delete the two new files and revert the two additive modifications; the Runtime is the prior
  Phase 3 state. No shared mutable state, no contract change.
