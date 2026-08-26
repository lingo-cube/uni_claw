## Context

See `proposal.md` for motivation. Authority baselines: UniAgent Architecture v1, Agent Concept Model v1, Runtime Architecture Contract I-1..I-14, the graduated Strategy Contract (`uniagent-runtimeagent-strategy-contract`), the graduated Phase 2 exploration ledger/depth capability (`runtime-exploration-ledger-and-depth-control` + successor), and the approved Phase 2.5 validation protocol (`PROJECT_LEADER_UNIAGENT_EMULATOR_VALIDATION_PROTOCOL_DESIGN`).

Current execution reality (verified at source):

- The Runtime accepts `run.strategy.start(StrategyRunStartRequest)` and executes one Strategy Run per accepted directive; admission is deterministic; one strategy creates at most one Run; completion remains Agent-owned GoalEvidence + FSM.
- The Emulator-facing read surface is the frozen Data plane: `run.list`, `run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`, `evidence.get`, `control.support` (+ `ping`). Events carry an audited A/B/C source classification; C-class events are never emitted.
- `ExplorationLedgerView` is not on the DriverHost wire; it exists only as the in-process public Agent read model (`Agent.CompileExplorationLedgerView()`). Full GoalEvidence is not on the public surface (`LatestGoalEvidence` is partial).
- `RunExecutionCoordinator` receives an injected `RunGraphFactory` (composition-root mapping `DeviceSelector → RunExecutionGraph`); the coordinator retains `Runs` as an in-process diagnostic view. Composition patterns live in `UniClaw.Runtime.PhysicalHost/PhysicalHostComposition.cs` (production, real-providers-only; fakes are forbidden there by F1).
- DriverHost E2E tests already host the loopback wire server in-process with deterministic worlds, proving the hosting pattern the harness needs.

## Goals / Non-Goals

**Goals:**

- A repeatable validation harness that executes the approved three scenarios against the existing, unmodified Runtime exactly through the existing wire contract and, on Tier A, the in-process public read model.
- Deterministic directive transport with full payload legality enforcement and an immutable call log.
- Truthful, tier-scoped evidence aggregation and reporting.
- Detectable boundary violations (no mutation / no FSM control / no action injection / no evidence fabrication).

**Non-Goals:**

- Any modification to `UniClaw.Runtime`, DriverHost transport, wire DTOs, or protocol versions.
- A Planner: the harness never infers a strategy from prose; directive authorship stays with the Emulator (agent loop) or recorded fixtures.
- Memory: S3 uses harness-local history to shape a new directive; nothing persists into or enters Runtime.
- UniAgent implementation, dynamic depth, multi-run orchestration as a runtime capability, or any Phase 2 frozen-capability change.
- Replacing the existing test suites or the phase lifecycle decisions.

## Decisions

### D1. One new tooling project, registered in the Runtime solution

Add `src/UniClaw.Runtime.ValidationHarness/` (net10.0, console-capable) to `src/UniClaw.Runtime.sln`. It references `UniClaw.Runtime`, `UniClaw.Runtime.Harness`, `UniClaw.Runtime.DriverHost`, and — for Tier A fixture semantics — the deterministic pieces of the existing Semantic/Adapter composition as needed.

Rationale: PhysicalHost is prohibited from hosting fakes (F1) and is a production entry; the harness must own its fixture compositions. A separate project keeps the validation tooling out of production and keeps Runtime production byte-identical. The reverse direction (harness → Runtime) is allowed exactly as the tests project references Runtime; the forbidden direction (Runtime → harness) never exists.

Alternative considered: implement the harness inside `tests/UniClaw.Runtime.Tests`. Rejected because the harness is also a runnable validation entry (Tier B/C against externally hosted DriverHost instances) with its own CLI surface, not a test-only assembly, and mixing it into the tests project would blur the "tests are evidence" boundary.

### D2. Emulator = existing Codex/dsh Agent Loop; harness is the loop's transport

The harness hosts the loop mechanics, not the intelligence:

- **Live mode**: the Project Leader / agent loop authors the `StrategyDirective`; the `EmulatorDriver` validates, transports via `run.strategy.start`, and logs.
- **Deterministic mode**: recorded directive fixtures (goal → directive records) drive repeatable Tier-A runs and capability tests.

The driver has exactly zero strategy-inference code; a missing directive produces `DIRECTIVE_REQUIRED`, never a synthesized strategy.

### D3. Tier A hosting: in-process DriverHost + fixture `RunGraphFactory`

The harness Tier-A composition starts `UniClawDriverHostServer` in-process (mirroring the existing E2E test hosting), with a `RunGraphFactory` that maps a fixture `DeviceSelector` to `RunExecutionGraph{ Agent, IEnvironment }` backed by deterministic fixture environments (capable of S2 anomaly injection).

S2 semantic revision (2026-08-26, Human decision REVISE_SPEC_WITHOUT_RUNTIME_CHANGE):
S2 validates Runtime Autonomous Exception Disposition, not recovery-always-succeeds.
AUTONOMOUS_HANDLING != RECOVERY_ALWAYS_SUCCEEDS and FAIL_CLOSED_TERMINAL !=
RECOVERY_FAILURE_OF_ARCHITECTURE. Two pass outcomes are defined: PASS_RECOVERED
(real recovery evidence + continued execution + zero Emulator intervention, when the
existing path has recovery capability) and PASS_BOUNDED_FAIL_CLOSED (Runtime-originated
terminal failure with explicit FailureReason backed by EvidenceRef/lifecycle events, no
unbounded retry, no hidden fallback, zero Emulator intervention). A bounded fail-closed
terminal is never labeled recovery success; absent recovery evidence is never fabricated.
Every S2 result records STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN /
NOT_PURCHASED_BY_PHASE_2_5 — the harness proves the upper layer need not intervene in
Run-internal exception disposition; it does not purchase strategy-path recovery capability.
A future recovery-and-continue buyer requires a separate Runtime Recovery capability via
OpenSpec + Human Gate, never via this harness. The Emulator loop still dials the loopback wire — the harness never bypasses the transport, so the validation exercises the real contract path including encoding.

Post-terminal, the Tier-A host reads `coordinator.Runs[runId].Graph.Agent` public read model to attest the Ledger view (existing read-only projection, same surface the Ledger spec designates).

### D4. Result schema is an aggregation, not a new contract

`ValidationResult` is harness-local: sections Admission / Lifecycle / Snapshot / Trap / Evidence / Coverage (tier-scoped) / Terminal / Boundary. Every field carries its truth-source classification (mirroring `RunSnapshot` field semantics: direct projection / derived read model / unavailable). No new wire message exists; the report is produced by the harness from the frozen surfaces.

### D5. Boundary proof is derived, not instrumented

No probes inside Runtime. Boundary evidence comes from:

1. Call log (client-side, immutable) — proves zero mutating calls and exact start counts.
2. Directive payload scans — prove no injected actions/coordinates/paths.
3. Event classification — all events received are A/B-class vocabulary.
4. `EvidenceRef` resolution through `evidence.get` — proves evidence provenance.

### D6. Failure classification reuses the protocol taxonomy

The harness labels any scenario failure with one of the protocol owners (Strategy Compilation / Discovery / Grounding / Authorization / Execution / Recovery / Environment / Test Harness) and records the First Divergence Point. A failure is never reported as a bare "Runtime failed."

### D7. Gates G1–G4 are checkable report fields

G1 = directive-legal (deterministic validation result). G2 = end-to-end autonomy (zero mid-run driver calls; terminal through existing path). G3 = every Result field traces to runtime surfaces. G4 = boundary verifier pass. Scenario files emit all four as explicit pass/fail entries.

## Authority proof

| Forbidden edge | Why impossible in this design | Required guard/proof |
|---|---|---|
| Harness → Runtime mutation | No mutating wire method exists; call log proves zero mutating calls; harness never receives Runtime powers | Call-log assertion + payload scan tests |
| Harness → FSM | No FSM surface is exposed to the harness; terminal events are emitted by Runtime only | Event-provenance tests (A/B classification only) |
| Harness → Action injection | Directive payloads are closed-enum validated before transport | `EMULATOR_DRIVER` unit tests for forbidden content |
| Harness → Evidence fabrication | Harness writes no capture records; every `EvidenceRef` resolves through `evidence.get` | Evidence resolution tests |
| Harness → Planner | No strategy inference code exists; missing directive yields `DIRECTIVE_REQUIRED` | Unit test + source-shape guard |
| Harness → new wire/API | Harness consumes existing surfaces only; frozen wire/DTO source is byte-identical after apply | GitHub-diff guard over frozen files (reuse existing wire guard pattern) |

## Stop-condition evaluation

The design requires no Runtime planning authority, no Agent/FSM/Traversal/GoalEvidence ownership change, no wire addition, no Memory, and no dynamic depth. It may therefore proceed to OpenSpec design (done here). Apply remains blocked on explicit Human Gate. Any future requirement for a new Runtime API, new wire contract, Phase 2 contract change, ownership adjustment, Planner, or Memory triggers the stop condition immediately.

## Risks / Trade-offs

- [Harness grows into a planner] → Directives are authored by the loop or fixtures only; driver validation code is strict and tested; `DIRECTIVE_REQUIRED` is the only prose handling.
- [Tier B/C needs device access] → Real-tier execution is gated by Human approval per the protocol; the Harness itself does not require it for capability tests.
- [Duplicate wire-client code] → Mirror the existing E2E loopback client pattern within the harness project; no extraction from tests into production is performed.
- [Coverage on wire tiers is partial] → Explicitly enforced truthfulness: unavailable is reported as unavailable, never guessed; full coverage assertions live in Tier A.
- [The harness is itself a new code surface] → Capability tests + boundary verifier cover the harness; guards keep it out of Runtime production.

## Migration Plan

1. Obtain explicit Human approval to apply this change (Human Gate).
2. Create the harness project and compose Tier-A hosting; no existing source is edited except `src/UniClaw.Runtime.sln` project registration.
3. Implement driver → collector → scenario runner → report → boundary verifier in bounded increments, each with capability tests.
4. Run the validation suite plus the full deterministic Runtime regression (harness tests must not disturb existing suites); run `openspec validate <change> --strict` and `scripts/check-consistency.sh`.
5. Produce the Phase 2.5 validation report as the change's output artifact; lifecycle conclusions (graduation/archival/resumption of Phase 3) remain exclusively human-owned.
6. Roll back by removing the harness project and its sln registration; no existing wire payload, lifecycle, or Runtime source needs migration.

## Design Docs

| Module / concern | Design Doc |
|---|---|
| Successor scope and buyer | `openspec/changes/uniagent-emulator-validation-harness/proposal.md` |
| Normative harness behavior | `openspec/changes/uniagent-emulator-validation-harness/specs/uniagent-emulator-validation-harness/spec.md` |
| Implementation steps | `openspec/changes/uniagent-emulator-validation-harness/tasks.md` |
| Approved validation protocol | `docs/decisions/` (Phase 2.5 protocol approval to be recorded by Human) |
| Strategy Contract authority | `openspec/changes/uniagent-runtimeagent-strategy-contract/specs/uniagent-runtimeagent-strategy-contract/spec.md` |
| Runtime authority | `docs/system/constitution/runtime-architecture-contract.md` |
| Runtime module map | `src/UniClaw.Runtime/AGENTS.md` |
| Test module map | `tests/UniClaw.Runtime.Tests/AGENTS.md` |