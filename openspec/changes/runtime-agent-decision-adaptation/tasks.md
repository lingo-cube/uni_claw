# Tasks: runtime-agent-decision-adaptation

> Implementation checklist. Each task is verifiable against
> `specs/runtime-agent-decision-adaptation/spec.md`. Order respects dependencies: model → adapter →
> ledger extension → integration → tests → regression → validate.

## 1. Hypothesis adaptation model

- [x] 1.1 Create `src/UniClaw.Runtime/Model/HypothesisAdaptation.cs`: `HypothesisAdaptationType` enum
      (Keep=1, Replace=2, Escalate=3) + sealed record `HypothesisAdaptation` (RunId, AdaptationType,
      DecisionReference, PreviousHypothesisReference, AdaptedHypothesis, AdaptationReason).
- [x] 1.2 Add construction-time validation: non-blank RunId, DecisionReference,
      PreviousHypothesisReference, AdaptationReason; AdaptationType defined; AdaptedHypothesis non-null.
      Reject invalid with ArgumentException.
- [x] 1.3 Assert the record carries NO Plan, DeviceAction, Tap, UI element, Goal modification, Traversal
      control, or scenario strings (model-level test in task 5).

## 2. Stateless hypothesis adapter

- [x] 2.1 Create `src/UniClaw.Runtime/Planning/HypothesisAdapter.cs`: `static` class with
      `Adapt(RuntimeDecision decision, ExecutionHypothesis currentHypothesis) → HypothesisAdaptation`.
      Pure function, no state, no authority.
- [x] 2.2 Implement Keep (Decision Continue): adapted hypothesis = current with Status Confirmed (if not
      already). No new assumption, no action, no Goal modification. Generic reason from decision reason.
- [x] 2.3 Implement Replace (Decision Revise): current hypothesis marked Replaced; new hypothesis Created
      with generic boundary-aware objective ("External boundary relation requires bounded return
      handling" — NOT a scenario string, NOT a SystemBack instruction). NO DeviceAction/Tap/SystemBack.
- [x] 2.4 Implement Escalate (Decision Escalate): adapted hypothesis = current with Status Revised +
      escalation-marked revision reason. NO recovery, NO retry, NO action dispatch. Records inability.
- [x] 2.5 Ensure NO scenario strings in any adaptation reason or adapted hypothesis objective — derive
      from decision reason + generic boundary/authority language only.

## 3. ExecutionHypothesisLedger extension (additive)

- [x] 3.1 Modify `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs`: add `Adapt() →
      HypothesisAdaptation` method — reads `LatestDecision` (Phase 3), delegates to
      `HypothesisAdapter.Adapt(LatestDecision, Current)`, applies `AdaptedHypothesis` to `_current`
      (appends to `_history` via the existing Append pattern), stores in `_latestAdaptation`, returns.
- [x] 3.2 Add `LatestAdaptation` property (HypothesisAdaptation? — null until Adapt is called).
- [x] 3.3 Confirm the ledger remains method-local (not assigned to any Agent/Container/Traversal/
      Environment field) — enforced by the authority test in task 6.
- [x] 3.4 Confirm history is preserved: Adapt appends to `_history` without rewriting prior entries
      (enforced by the history test in task 5).

## 4. DirectiveExecution integration (additive, no signature change)

- [x] 4.1 Modify `src/UniClaw.Runtime/Planning/DirectiveExecution.cs`: inside the existing ContinueWith
      (when hypothesisLedger is non-null), after `Reconcile` (Phase 3), add one call:
      `hypothesisLedger.Adapt();`. No signature change to RunDirectiveAsync.
- [x] 4.2 Confirm `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`,
      `Recovery/`, `World/`, `IntentExecution.cs`, `HypothesisReconciler.cs` are byte-unchanged
      (diff review).

## 5. Unit tests

- [x] 5.1 `HypothesisAdaptationTests`: construction exposes only adaptation fields; rejects blank
      RunId/AdaptationReason/refs + undefined Type; enum exhaustive; carries no Action/authorization/UI/
      Goal/Traversal/scenario-string (surface assertion).
- [x] 5.2 `HypothesisAdapterTests`: Keep (Continue → current with Confirmed; no new assumption);
      Replace (Revise → current Replaced + new Created with boundary-aware objective; NO SystemBack/
      DeviceAction/Tap); Escalate (Escalate → current Revised + escalation reason; NO recovery/retry).
      Deterministic (two adaptations structurally identical). Stateless. No scenario strings.
- [x] 5.3 `HypothesisAdaptationRunLocalIsolationTests`: LatestAdaptation per-run; two separate runs
      independent; ledger not retained in any Agent/Container/Traversal/Environment field (reflection).
- [x] 5.4 `HypothesisAdaptationHistoryTests`: Adapt appends to History without rewriting prior entries;
      the full sequence (initial → revised → replaced → adapted) remains observable.

## 6. Authority tests

- [x] 6.1 `HypothesisAdaptationAuthorityTests`: the adaptation model and adapter expose NO method that
      authorizes, executes, dispatches, creates a container, or initiates a sub-run (forbidden-name
      reflection). The adapter exposes ONLY `Adapt`.
- [x] 6.2 Replace does NOT execute SystemBack or any DeviceAction (assert no DeviceAction/Tap/SystemBack
      in the adaptation or adapter output).
- [x] 6.3 Escalate does NOT recover or retry (assert no Recovery/system-back/dispatch in the adaptation).
- [x] 6.4 The RunState is produced by the Agent's existing DFS engine, not by the adaptation (Fake-env
      end-to-end: run with ledger → assert RunState equals DFS result; adaptation only records).
- [x] 6.5 The GoalEvidence is evaluated by the existing evaluator, not by the adaptation.
- [x] 6.6 The Agent authorization path does not reference the adaptation (source assertion).

## 7. Scenario tests (Fake World)

- [x] 7.1 `AdaptationScenario1KeepTests`: hypothesis "navigate recursive child" + observation expected
      child reached + Decision Continue → Keep adaptation (hypothesis remains active/confirmed). Assert
      execution authority unchanged.
- [x] 7.2 `AdaptationScenario2ReplaceTests`: hypothesis "recursive child expected" + observation external
      boundary + Decision Revise → Replace adaptation (new hypothesis records boundary interpretation;
      NO action execution; existing ExternalBoundary capability handled it inside the DFS loop).
- [x] 7.3 `AdaptationScenario3EscalateTests`: hypothesis "execution possible" + observation authority
      boundary exceeded + Decision Escalate → Escalate adaptation (records inability; NO automatic
      recovery).

## 8. Regression guard

- [x] 8.1 Run `dotnet build src/UniClaw.Runtime.sln` — 0 errors, 0 warnings (isolate from concurrent
      broken `Capabilities/Perception/Semantic/` if needed via quarantine-verify-restore).
- [x] 8.2 Run `dotnet test src/UniClaw.Runtime.sln` — all deterministic suites green (1596+), including
      SETTINGS-TREE-01 capstone (TREE-1..TREE-20), U2OpenWorld, OpenWorldTypeDirected, Phase 1 Directive
      tests, Phase 2 ExecutionHypothesis tests, Phase 3 RuntimeDecision tests, ArchitectureGuardTests.
      Only pre-existing env-gated RealDevice/RealEmulator + the concurrent scroll-guard may fail.
- [x] 8.3 Confirm `scripts/check-consistency.sh` ALL PASS and `git diff --check` clean.

## 9. OpenSpec validate

- [x] 9.1 Run `openspec validate runtime-agent-decision-adaptation --strict` — passes.
- [x] 9.2 Update this `tasks.md` checkbox state as each task completes.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Planning/` | [docs/system/layers/planning.md](../../docs/system/layers/planning.md) |
| `src/UniClaw.Runtime/Model/` (immutable models) | [docs/system/greenfield-runtime-charter.md](../../docs/system/greenfield-runtime-charter.md) §40 + `src/UniClaw.Runtime/AGENTS.md` directory table |
| `src/UniClaw.Runtime/Agent/` (execution/FSM authority, unchanged) | [docs/system/layers/agent-runtime.md](../../docs/system/layers/agent-runtime.md) |
