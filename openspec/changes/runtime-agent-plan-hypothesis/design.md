# Design: runtime-agent-plan-hypothesis

> HOW to implement the run-local, revisable execution hypothesis. See `proposal.md` for motivation and
> `specs/runtime-agent-plan-hypothesis/spec.md` for the behavior contract. This design adds an
> immutable model + a method-local ledger + an optional parameter, and reuses the existing DFS engine
> unchanged.

## Context

The RuntimeAgent's DFS engine (`Agent.RunOpenWorldAsync`) records every execution inflection point as
a `TraceEvent` in the Agent's append-only `_trace` list — discovery epoch freeze, authorization
reject, external boundary observed, verified parent return, leaf dispatch, depth cutoff, completion.
The trace is a public, real-time evidence stream (`Agent.Trace`) that does not drive decisions.

Existing run-local observable records owned by Agent (I-2): `_trace` (TraceEvent list), `_belief`
(WorldBelief), `_branchProgress`. All are passive records / snapshots; decisions remain the Agent's.

The Planning layer doc states: **"Planning owns no mutable Runtime state."** So any ledger in
Planning/ must be a transient derivation, not Runtime state. The established pattern is
`Reconcile.FromObservation` (World/) — a pure function deriving a WorldBelief from an Observation,
not state that the Runtime consults.

The Phase 1 `DirectiveExecution.RunDirectiveAsync` is the additive entry point (Planning/) that wraps
a decomposed directive in an open-world envelope and calls the existing `IntentExecution` seam.

## Goals / Non-Goals

**Goals:**
- Provide an immutable `ExecutionHypothesis` record + `ExecutionHypothesisStatus` lifecycle.
- Provide a run-local, method-local `ExecutionHypothesisLedger` that creates the initial hypothesis
  from a decomposed directive and revises the hypothesis sequence from `Agent.Trace` + `RunState`.
- Integrate additively into `DirectiveExecution.RunDirectiveAsync` via an optional nullable
  parameter (null = zero regression).
- Deterministic tests: unit (lifecycle), authority (passivity), scenario (boundary revision).

**Non-Goals:**
- Real-time mid-loop hypothesis revision (would require modifying the DFS loop with an observer
  pattern). Out of scope; a separate follow-up if required. Post-run, trace-derived revision
  satisfies the proof goal.
- Agent-observable hypothesis state (adding a field to Agent). The hypothesis is observed via the
  ledger returned/retained by the caller, not via an Agent property. Avoids adding Runtime state.
- Wiring the hypothesis into the closed-world `RunSemanticGoalAsync` path or the `RunStartRequest`
  wire surface. The directive path is the scope.
- Global plan store, persistent hypothesis, navigation graph, scenario knowledge, LLM planning —
  explicitly forbidden by the mission and the frozen invariants.

## Decisions

### Decision 1: `ExecutionHypothesis` is an immutable record in `Model/`, analogous to `TraceEvent`
**Choice:** `src/UniClaw.Runtime/Model/ExecutionHypothesis.cs` — sealed record + `ExecutionHypothesisStatus`
enum, construction-time validation, no methods beyond accessors.
**Rationale:** Matches `Model/`'s role (pure immutable models, no owner) and the existing
`TraceEvent`/`GoalEvidence`/`WorldBelief` placement. The hypothesis is a passive observable record,
structurally identical in kind to TraceEvent. No new component with architecture meaning.
**Alternatives considered:** placing it in `Planning/` (rejected — it is a model, and Model/ is the
canonical home for immutable records); making it a struct (rejected — records give value equality and
`with`-based revision, matching the codebase style).

### Decision 2: The ledger is method-local in `Planning/`, NOT Runtime state
**Choice:** `src/UniClaw.Runtime/Planning/ExecutionHypothesisLedger.cs` — a class instantiated as a
method-local variable in `RunDirectiveAsync`, holding the current hypothesis + an immutable history
list. It is created per run, used to derive the hypothesis sequence from evidence, and discarded when
the method returns.
**Rationale:** The Planning layer doc mandates "Planning owns no mutable Runtime state." A method-local
ledger is a transient computation (like a local `List<T>` or a `Reconcile` result), not Runtime state
owned by Agent/Container/Traversal/Environment. It does not survive the method. This respects I-2
(no new Runtime state owner) and the Planning layer boundary.
**Alternatives considered:** an Agent field `_hypothesisLedger` (rejected — adds Runtime state to
Agent, requires DFS-loop modification to update, and blurs the "DFS engine unchanged" guarantee);
an interface `IExecutionHypothesisLedger` (rejected — YAGNI, no second implementation, no test-seam
need since it is a pure derivation class).

### Decision 3: Hypothesis revision is trace-derived (post-run), not real-time
**Choice:** After `IntentExecution.RunOpenWorldAsync` returns, the ledger scans `Agent.Trace` for
inflection-point evidence (boundary observed, verified return, inventory complete, completion) and
maps each to a hypothesis lifecycle transition (Confirm / Revise / Replace). The run outcome
(RunState) determines the final hypothesis status.
**Rationale:** The trace is the existing real-time evidence stream, recorded at every loop inflection
point without driving decisions. Deriving the hypothesis from the trace post-run is evidence-driven
(I-4: observation is evidence) and requires ZERO modification to the proven DFS engine. This is the
smallest insertion point (per the mission: "DirectiveExecution → RuntimeAgent Context →
ExecutionHypothesis → IntentExecution"). Real-time revision would require modifying `Agent.OpenWorld.cs`
with an observer/callback — a larger change the mission discourages ("Do not modify these").
**Alternatives considered:** real-time observer callbacks in the DFS loop (rejected — modifies the
proven loop, larger regression surface); deriving the hypothesis purely from RunState without the
trace (rejected — loses the inflection-point evidence that makes revision meaningful).

### Decision 4: Optional nullable parameter, zero-regression default
**Choice:** `DirectiveExecution.RunDirectiveAsync` gains `ExecutionHypothesisLedger? hypothesisLedger
= null`. When null: existing Phase 1 behavior, no hypothesis created. When provided: the ledger is
populated with the initial hypothesis before the run and revised from trace + outcome after.
**Rationale:** Additive, zero-regression by construction. The Phase 1 authority test calls
`RunDirectiveAsync` without the ledger → existing behavior. New tests provide the ledger. No existing
signature is broken (optional parameter with default).
**Alternatives considered:** a new overload `RunDirectiveWithHypothesisAsync` (rejected — duplicates
the method); changing the return type to include the ledger (rejected — breaks the Phase 1 test
unnecessarily; the caller retains the ledger reference they passed in).

### Decision 5: The ledger derives the objective/transition from the directive's declared boundaries, not scenario knowledge
**Choice:** The initial hypothesis objective is "Explore declared scope within bounded depth"
(derived from `Directive.Scope` + `Directive.MaximumDepth` + `Directive.Completion`), and the expected
transition is "Discover → Authorize → Expand" (derived from the DFS engine's documented phases). No
scenario strings (no "Settings," no "Location," no "Battery").
**Rationale:** Respects "RuntimeAgent MUST NOT know Settings-specific rules / contain scenario
strings." The hypothesis is generic and type-directed, derived from the directive's declared
boundaries. The revision reasons are derived from trace event reasons (which are already generic:
"EXTERNAL_BOUNDARY_OBSERVED," "verified parent return," etc.).
**Alternatives considered:** caller-injected objective text (rejected — adds caller surface
unnecessarily; the directive's boundaries already define the objective).

## Risks / Trade-offs

- **[Risk] Post-run revision is weaker than real-time revision** → Mitigation: the proof goal is
  "maintain and revise ... without gaining new authority," which post-run trace-derived revision
  satisfies. The trace IS the real-time record; the hypothesis is a higher-level interpretation. If
  real-time revision is later required, it is a separate additive-observer change with its own review.
- **[Risk] Ledger accidentally becomes Runtime state** → Mitigation: the ledger is method-local by
  construction (a local variable in `RunDirectiveAsync`), never assigned to an Agent/Container/
  Traversal/Environment field. A dedicated authority test asserts no Agent field references it and
  the RunState is produced by the Agent, not the ledger.
- **[Risk] Hypothesis drifts toward a God Context (I-13)** → Mitigation: `ExecutionHypothesis` is a
  narrow sealed record with only assumption fields; it is not aggregated with Observation /
  WorldBelief / RuntimeState / Memory. The ledger holds only the hypothesis sequence, nothing else.
- **[Risk] Regression to Phase 1 directive or proven DFS** → Mitigation: optional parameter defaults
  to null (zero regression); DFS engine untouched. The regression guard (SETTINGS-TREE-01, U2OpenWorld,
  OpenWorldTypeDirected, Phase 1 directive tests) must stay green.
- **[Trade-off] No Agent-observable hypothesis property** → Acceptable: the hypothesis is observed via
  the ledger retained by the caller/test, not via an Agent property. This avoids adding Runtime state
  and keeps the Agent surface unchanged.

## Migration Plan

- Additive only; no removal or rename. `DirectiveExecution.RunDirectiveAsync` gains an optional
  parameter (default null). The Phase 1 authority test is updated to confirm the optional parameter
  doesn't change existing behavior.
- Deploy: build `src/UniClaw.Runtime.sln`; run `dotnet test`. Existing suites must pass unchanged.
  New deterministic tests cover the model, ledger, authority, and scenario.
- Rollback: delete the two new files and revert the one optional-parameter addition; the Runtime is
  the prior Phase 1 state. No shared mutable state, no contract change.
