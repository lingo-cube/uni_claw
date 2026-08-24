# Proposal: runtime-agent-plan-hypothesis

> Change ID: `runtime-agent-plan-hypothesis`
> Status: Proposed
> Type: Capability extension (additive, no contract/invariant change, no DFS-loop modification)
> Baseline verified: 2026-08-21, branch `uni-agent`, build 0 errors / 0 warnings, 1506 deterministic tests green
> Authority decision: Leader review passed — NONE authority impact. The hypothesis is a passive,
> run-local, trace-derived record; the Agent keeps sole authority; the DFS engine is unchanged.

## Why

After Phase 1 (`runtime-agent-directive-capability`), a bounded exploration directive can enter
Runtime execution and feed the proven DFS engine. But the RuntimeAgent does not maintain an **explicit,
run-local execution hypothesis** — a first-class record of its current execution assumption (objective,
expected transition, expected outcome) and how observations confirmed or revised it. The DFS loop's
assumptions are implicit in its control flow and recorded only as flat trace events; they are not
made explicit as a lifecycle-tracked, revisable hypothesis model.

The mission's proof goal: "RuntimeAgent can maintain and revise a run-local execution hypothesis
without gaining new authority." The missing piece is the hypothesis model + a run-local ledger that
creates it from a directive and revises it from execution evidence (the Agent's existing trace +
run outcome).

## What Changes

- **NEW** immutable `ExecutionHypothesis` model (`Model/`) — a passive record of one execution
  assumption: RunId, DirectiveReference, Objective, ExpectedTransition, ExpectedOutcome, Confidence,
  RevisionReason, CreatedAtObservation, Status. Carries NO Plan, coordinates, DeviceAction, element
  index, scenario strings, or authority.
- **NEW** `ExecutionHypothesisStatus` enum — lifecycle: Created → Active → Confirmed | Revised →
  Replaced.
- **NEW** `ExecutionHypothesisLedger` (`Planning/`) — a run-local, method-local, transient derivation
  that creates the initial hypothesis from a decomposed directive and revises the hypothesis sequence
  from the Agent's trace evidence + run outcome. It is NOT Runtime state (Planning owns no mutable
  Runtime state); it is a pure computation discarded when the run method returns. It holds NO
  authority: it cannot authorize, decide, complete, or execute.
- **MODIFIED** `DirectiveExecution.RunDirectiveAsync` (additive) — optional `ExecutionHypothesisLedger?`
  parameter (default null). When provided: create initial hypothesis, run the DFS via the existing
  unchanged seam, revise the hypothesis from `Agent.Trace` + `RunState`. When null: existing Phase 1
  behavior, zero regression.
- **UNCHANGED**: `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`,
  `Recovery/`, `World/`, `IntentExecution.cs`, all contracts, all frozen invariants. The DFS engine
  is not modified.
- **NEW** deterministic tests: unit (creation, lifecycle, confirmation, revision, run-local
  isolation), authority (cannot authorize / bypass Agent / modify completion / create recursive
  authority), scenario (Fake World: directive → hypothesis → boundary observation → revision →
  execution authority unchanged).
- **NOT changed**: Architecture v1, Protocol v1, Contract I-1..I-14, charter, `RunStartRequest`,
  `Agent.RunOpenWorldAsync` signature, any frozen decision. No new state owner, no new decision
  authority, no new persistent component.

## Capabilities

### New Capabilities
- `runtime-agent-plan-hypothesis`: run-local, revisable `ExecutionHypothesis` model +
  `ExecutionHypothesisLedger` that creates the hypothesis from a decomposed directive and revises it
  from execution evidence (trace + outcome), integrated additively into the existing
  `DirectiveExecution` entry. Owns no authority; the DFS engine and Agent authority are unchanged.

### Modified Capabilities
<!-- None. The Phase 1 runtime-agent-directive-decomposition capability is extended additively
(optional parameter), not spec-level modified. The downstream open-world execution capabilities are
unchanged. -->

## Impact

- **Code**: NEW `src/UniClaw.Runtime/Model/ExecutionHypothesis.cs`; NEW
  `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs`; MODIFIED
  `src/UniClaw.Runtime/Planning/DirectiveExecution.cs` (additive optional parameter).
  `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/` — **unchanged**.
- **APIs**: additive only. `DirectiveExecution.RunDirectiveAsync` gains an optional nullable
  parameter (default null = existing behavior). No existing signature is broken; the Phase 1
  authority test is updated to accommodate the optional parameter. `Agent.RunOpenWorldAsync` is
  untouched.
- **Dependencies**: none new. Stays inside `UniClaw.Runtime` (ArchitectureGuardTests Guard 1/2).
- **Authority**: NONE. The hypothesis is a passive record; the ledger is a transient derivation, not
  Runtime state. The Agent keeps sole run-level semantic/execution authority; the DFS engine is
  unchanged. Verified against v1 invariants 2-4, Contract I-2/I-3/I-5/I-12/I-13.
- **Tests**: NEW deterministic tests under `tests/UniClaw.Runtime.Tests/`; Phase 1 directive tests
  and all existing suites must remain green.
- **Risk**: Low — additive model + method-local ledger + optional parameter; DFS loop untouched.
