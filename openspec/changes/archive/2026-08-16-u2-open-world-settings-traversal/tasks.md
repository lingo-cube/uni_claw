## 1. Deterministic Scenario Infrastructure

- [x] 1.1 **Create the SC-U2-MUS-001 deterministic open-world Settings fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-U2-MUS-001
  - **Goal:** Add test-only deterministic worlds and wiring for root siblings A/B, explicit parent returns, a dangerous visible candidate, a beyond-depth candidate, unresolved inventory, ambiguous/rejected return, wrong parent, stale Observation, A-only progress, and equal-input replay.
  - **Required Semantic:** The Fake owns only visible world state, transitions, dispatch outcomes, and Observations. It MUST NOT encode semantic Container identity, branch completion, bounded traversal completion, Goal success, or action authority.
  - **Approved Production Purchase:** Production delta = 0 for this task.
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/Fakes/**`, focused fixture tests, and this task progress.
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`; implementation of the open-world control flow; Planner/FSM/Graph/route model; new semantics outside SC-U2-MUS-001.
  - **Assertions:** A/B are absent from a Plan; root and child worlds replay deterministically; explicit parent target variants are expressible; dangerous/deeper candidates remain observable; fresh/stale/wrong-parent outcomes are independently scriptable; Goal criteria are deterministic over supplied immutable evidence.
  - **Verification:** targeted fixture tests and deterministic replay comparison.
  - **Deferred Boundary:** Runtime execution, completion evaluation, viewport discovery, generic navigation, Recovery, Popup, Harness, and another usability slice.

## 2. Bounded Production Composition

- [x] 2.1 **Implement the authorized upstream seam and bounded Agent traversal**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-U2-MUS-001
  - **Goal:** Execute a resolved navigation-only `OPEN_WORLD_TYPE_LEVEL` envelope through dynamic A/B discovery, verified child terminal evidence, unique authorized parent return, fresh exact parent reconciliation, sibling continuation, and final existing fresh GoalEvidence.
  - **Required Semantic:** Agent derives `VerifiedBoundedTraversalCompletion` as a semantic condition before invoking the existing `Goal.EvidenceEvaluator` on the fresh root Observation; Traversal retains local select/dispatch/fresh verification; non-traversal completion remains unchanged.
  - **Approved Production Purchase:** exactly one new `Planning/IntentSemanticEnvelopeExecution.cs` public static seam and bounded changes to existing `Agent.cs`; `Goal.cs` and all existing evidence models remain unchanged; no other production file.
  - **Allowed Scope:** `src/UniClaw.Runtime/Planning/IntentSemanticEnvelopeExecution.cs`, `src/UniClaw.Runtime/Agent/Agent.cs`, focused Planning/API/architecture tests, and this task progress.
  - **Forbidden Scope:** modification of `Goal.cs`; addition of `Goal.BranchProgressEvidenceEvaluator`; change to existing `Agent.RunAsync` signature/closed-world semantics; Agent dependency on Planning; new frame type/enum/interface/engine/manager/mutable field/owner; Planner/Compiler engine/FSM/Graph/route model/new Back action; Container/Traversal/Environment/Recovery production changes.
  - **Assertions:** the seam rejects closed-world input before Runtime activity; no concrete Plan/route/inventory is manufactured; exactly one pending required child is nominated at a time; unique positive parent return is required pre-dispatch; wrong/stale evidence records no completion; A remains complete while B is pending; existing Goal evaluation is never called before verified bounded traversal completion; only satisfied fresh GoalEvidence after that condition completes.
  - **Verification:** focused unit/API tests, positive/negative Scenario behavior, Architecture Guard proving Agent has no Planning dependency, and production-delta audit.
  - **Deferred Boundary:** viewport discovery, natural-language parsing, generic route planning/backtracking, state-changing tasks, generic navigation/retry/uncertainty, Recovery/Popup, Harness, and U3.

## 3. Formal Scenario Proof

- [x] 3.1 **Prove SC-U2-MUS-001 positive, negative, cutoff, completion, and replay branches**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-U2-MUS-001
  - **Goal:** Establish the L2 production-shaped proof across Planning → Agent → Container → Traversal → Environment.
  - **Required Semantic:** intermediate progress, visited known nodes, local exhaustion, observation failure, ambiguity, and depth/safety cutoff do not independently complete; cutoff reasons do not claim discovered-world exhaustion.
  - **Approved Production Purchase:** Production behavior/model delta = 0 for this task; formal tests only.
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**`, focused regression tests, Scenario receipt/evidence progress, and this tasks file.
  - **Forbidden Scope:** production redesign/repair beyond an explicit bounded implementation bug; another Scenario; Harness; new semantic or architecture purchase.
  - **Assertions:** positive run performs exactly four Taps and completes only after A/B verified returns; unresolved inventory, A-only pending B, ambiguous/rejected return, wrong parent, stale Observation, and unsatisfied final GoalEvidence do not fabricate completion; dangerous/deeper candidates receive zero dispatch; replay outputs are equal.
  - **Verification:** targeted formal Scenario and replay tests plus CP-04/07/12/14 and frozen Phase 1–3 regressions.
  - **Deferred Boundary:** all capabilities outside the declared navigation-only depth-bounded explicit-parent-target slice.

## 4. Full Validation and Evidence Promotion

- [x] 4.1 **Independently validate the U2 slice, exact delta, boundaries, regressions, and evidence promotion**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-U2-MUS-001
  - **Goal:** Freshly audit actual diff and executable evidence, then promote the L2 Scenario only when every required validation passes.
  - **Required Semantic:** traversal-shaped completion follows the Human-frozen condition; desired-world-state completion and all ownership/authority boundaries remain unchanged.
  - **Approved Production Purchase:** one added production file; modified `Agent.cs` only; one public static seam; zero Goal/model values and zero new enum/interface/engine/manager/mutable field/owner.
  - **Allowed Scope:** read-only production/test/spec audit; required verification commands; Tier 1/2/3 documentation sync if mechanically required; completion receipt and tasks progress after PASS.
  - **Forbidden Scope:** unapproved production repair, architecture redesign, another usability slice, Runtime expansion, Harness modification, or lifecycle graduation beyond U2.
  - **Assertions:** scenario/spec/design/implementation align; Agent has no Planning dependency; Traversal/Container/Environment/Recovery ownership is unchanged; closed/open/insufficient CP-14 semantics remain intact; all deferred capabilities remain absent; evidence replay is deterministic.
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`; targeted U2 tests; Architecture Guards; full `dotnet test src/UniClaw.Runtime.sln`; `scripts/check-consistency.sh`; `openspec validate u2-open-world-settings-traversal --strict`; `openspec validate --all --strict`; whitespace/static scope audit.
  - **Validation Receipt:** Independent Luna read-only validation PASS: U2 18/18, Architecture Guards 9/9, focused frozen regressions 15/15, full suite 484/484, OpenSpec 14/14 strict, consistency C1–C10 ALL PASS, `git diff --check` PASS; completion receipt at `docs/decisions/u2-minimum-usable-agent-slice-result.md`.
  - **Deferred Boundary:** no U3, device/emulator lane, viewport expansion, generic planner/navigation, state-changing open-world work, or architecture change.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Planning/` | `openspec/changes/u2-open-world-settings-traversal/design.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `openspec/changes/u2-open-world-settings-traversal/scenarios/SC-U2-MUS-001-open-world-settings-traversal.md` |
