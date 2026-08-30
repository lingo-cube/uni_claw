# Tasks: runtime-agent-plan-hypothesis

> Implementation checklist. Each task is verifiable against
> `specs/runtime-agent-plan-hypothesis/spec.md`. Order respects dependencies: model → ledger →
> integration → tests → regression → validate.

## 1. Execution hypothesis model

- [x] 1.1 Create `src/UniClaw.Runtime/Model/ExecutionHypothesis.cs`: `ExecutionHypothesisStatus` enum
      (Created=1, Active=2, Confirmed=3, Revised=4, Replaced=5) + sealed record `ExecutionHypothesis`
      (RunId, DirectiveReference, Objective, ExpectedTransition, ExpectedOutcome, Confidence,
      RevisionReason?, CreatedAtObservation?, Status).
- [x] 1.2 Add construction-time validation: non-blank RunId, DirectiveReference, Objective,
      ExpectedTransition, ExpectedOutcome; Confidence in [0,1]; Status defined. Reject invalid with
      ArgumentException.
- [x] 1.3 Assert the record carries NO Plan, coordinates, DeviceAction, element index, scenario
      strings, or authorization rules (model-level test in task 4).

## 2. Run-local hypothesis ledger

- [x] 2.1 Create `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs`: a run-local class
      holding the current `ExecutionHypothesis` + an immutable history list. Constructor takes a
      `DirectiveDecompositionResult.Resolved` + runId and creates the initial hypothesis (Status
      Created) from the directive's declared scope, maximum depth, and completion requirement — NO
      scenario strings.
- [x] 2.2 Add `Activate()` → sets current hypothesis to Active. Add `ReviseFromEvidence(IReadOnlyList
      <TraceEvent> trace, RunState outcome)` → scans trace inflection points (boundary observed,
      verified return, inventory complete, completion/fail) and produces the revised hypothesis
      sequence: Confirm on matching observations, Revise (with reason from trace) on contradictions,
      Replace when a revised hypothesis is superseded. Final status from RunState outcome.
- [x] 2.3 Expose `Current` (the latest hypothesis) + `History` (immutable snapshot of all hypotheses
      in the sequence) for test observability. The ledger holds NO authority methods (no authorize,
      no decide, no complete, no execute, no Agent-state mutation).
- [x] 2.4 Confirm the ledger is method-local by construction: it is never assigned to an
      Agent/Container/Traversal/Environment field (enforced by the authority test in task 5).

## 3. Additive DirectiveExecution integration

- [x] 3.1 Modify `src/UniClaw.Runtime/Planning/DirectiveExecution.cs`: add optional parameter
      `ExecutionHypothesisLedger? hypothesisLedger = null` to `RunDirectiveAsync`. When null: existing
      Phase 1 behavior, zero regression.
- [x] 3.2 When the ledger is provided: call `ledger.Activate()` before `IntentExecution.RunOpenWorldAsync`,
      then after it returns call `ledger.ReviseFromEvidence(agent.Trace, result)` where `result` is the
      returned RunState. The DFS engine call is UNCHANGED.
- [x] 3.3 Confirm `Agent.OpenWorld.cs`, `Agent.cs`, `Agent.Recovery.cs`, `Container/`, `Traversal/`,
      `Recovery/`, `World/`, `IntentExecution.cs` are byte-unchanged (diff review).

## 4. Unit tests

- [x] 4.1 `ExecutionHypothesisTests`: construction exposes only assumption fields; rejects blank
      RunId/Objective; rejects Confidence outside [0,1]; carries no Plan/coordinates/DeviceAction/
      index/scenario-string (reflective or surface assertion).
- [x] 4.2 `ExecutionHypothesisLifecycleTests`: lifecycle states are exactly Created/Active/Confirmed/
      Revised/Replaced; a revised hypothesis records a non-blank RevisionReason; a replaced hypothesis
      is superseded by a new Created hypothesis.
- [x] 4.3 `ExecutionHypothesisLedgerTests`: ledger creates an initial hypothesis (Status Created) from
      a decomposed directive with NO scenario strings; Activate → Active; ReviseFromEvidence maps trace
      inflection points to Confirm/Revise/Replace; final status from RunState outcome; history is an
      immutable snapshot of the sequence.
- [x] 4.4 `ExecutionHypothesisRunLocalIsolationTests`: the ledger is not retained in any Agent/
      Container/Traversal/Environment field after the run method returns; two separate runs produce
      independent ledgers with no cross-contamination.

## 5. Authority tests

- [x] 5.1 `ExecutionHypothesisAuthorityTests`: the hypothesis and ledger expose NO method that
      authorizes an action or produces authorization evidence; the Agent's authorization path does not
      reference the hypothesis (grep/source assertion).
- [x] 5.2 The RunState is produced by the Agent's existing DFS engine, not by the hypothesis or ledger
      (Fake-env end-to-end: run with ledger → assert RunState equals the DFS engine's result; the
      ledger only records, never decides).
- [x] 5.3 The GoalEvidence is evaluated by the existing evidence evaluator, not by the hypothesis
      (assert the hypothesis status reflects the outcome but does not determine it).
- [x] 5.4 The hypothesis and ledger expose NO method that dispatches an action, creates a container,
      or initiates a sub-run (no recursive authority).

## 6. Scenario test (Fake World)

- [x] 6.1 `ExecutionHypothesisBoundaryRevisionScenarioTests`: Fake World — directive "explore bounded
      environment" → initial hypothesis "explore declared scope" → DFS encounters an external boundary
      (trace records `EXTERNAL_BOUNDARY_OBSERVED`) → hypothesis revised (RevisionReason derived from
      the boundary trace event) → DFS handles boundary disposition, returns to parent, continues →
      hypothesis replaced by "continue siblings" → final outcome. Assert: hypothesis revised correctly
      (the history shows the revision with the boundary reason); execution authority unchanged
      (RunState from Agent DFS, not from hypothesis).

## 7. Regression guard

- [x] 7.1 Run `dotnet build src/UniClaw.Runtime.sln` — 0 errors, 0 warnings.
- [x] 7.2 Run `dotnet test src/UniClaw.Runtime.sln` — all deterministic suites green (1506+), including
      SETTINGS-TREE-01 capstone (TREE-1..TREE-20), U2OpenWorld, OpenWorldTypeDirected,
      BoundedCandidateSafety, BoundedCrossPageDiscovery, Phase 1 directive tests, ArchitectureGuardTests.
      Only pre-existing env-gated RealDevice/RealEmulator tests may fail (no emulator in sandbox).
- [x] 7.3 Confirm `scripts/check-consistency.sh` ALL PASS and `git diff --check` clean.

## 8. OpenSpec validate

- [x] 8.1 Run `openspec validate runtime-agent-plan-hypothesis --strict` — passes.
- [x] 8.2 Update this `tasks.md` checkbox state as each task completes.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Planning/` | [docs/system/layers/planning.md](../../../docs/system/layers/planning.md) |
| `src/UniClaw.Runtime/Model/` (immutable models) | [docs/system/greenfield-runtime-charter.md](../../../docs/system/greenfield-runtime-charter.md) §40 + `src/UniClaw.Runtime/AGENTS.md` directory table |
| `src/UniClaw.Runtime/Agent/` (execution authority, unchanged) | [docs/system/layers/agent-runtime.md](../../../docs/system/layers/agent-runtime.md) |
