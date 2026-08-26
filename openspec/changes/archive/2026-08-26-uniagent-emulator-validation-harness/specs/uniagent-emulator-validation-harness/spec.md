## Purpose

Defines the Phase 2.5 UniAgent Emulator Validation Harness: validation tooling that drives the existing, unmodified RuntimeAgent from an external abstract strategy loop and produces evidence-backed validation results, while remaining incapable of Runtime mutation, FSM control, action injection, or evidence fabrication. The harness is a validation tool — not a UniAgent, not a Planner, not Memory, and not a new Runtime capability.

## ADDED Requirements

### Requirement: Validation tooling, never runtime or planning capability

The harness SHALL be a validation tool that consumes only existing surfaces: the `run.strategy.start` admission operation and the frozen read-only wire surface (`run.list`, `run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`, `evidence.get`, `control.support`), plus — only in its in-process Tier-A composition — the existing public Agent read model. The harness MUST NOT add a wire method, add a Runtime API, modify `StrategyDirective` or `run.strategy.start` semantics, change Phase 2 authority, implement a Planner, implement Memory, or implement UniAgent. Any discovered pressure to do any of these SHALL stop the change and request a Human Gate instead of self-extending.

#### Scenario: Harness lives outside Runtime

- **WHEN** the harness project and the Runtime production solution are inspected
- **THEN** the harness compiles against `UniClaw.Runtime`, `UniClaw.Runtime.DriverHost`, and `UniClaw.Runtime.Harness` only in client/host roles, and no Runtime production source file is modified by this change

#### Scenario: Contract-frozen surfaces untouched

- **WHEN** the frozen `StrategyDirective`, `run.strategy.start` payload, wire DTOs, and public protocol versions are compared before and after harness application
- **THEN** they are byte-identical and the change introduces no new wire contract

### Requirement: Emulator driver boundary

The harness SHALL accept a human-readable Goal plus a `StrategyDirective` — authored either by the existing Codex/dsh Agent Loop or supplied as a recorded fixture in deterministic mode — validate the directive against the closed Strategy vocabulary, transport it through the existing `run.strategy.start`, and record an immutable call log entry (method, payload digest, admission result, timestamp). The harness MUST NOT author a strategy from user prose, MUST NOT generate coordinates, page paths, actions, click sequences, element locators, callbacks, or runtime state, and MUST reject any directive containing such content before transport.

#### Scenario: Legal directive is transported

- **WHEN** a directive's fields all fall inside the closed `StrategyDirective` vocabulary and carry no forbidden payload content
- **THEN** the driver transports it via `run.strategy.start` and records the admission result (`Accept` with `runId`, or deterministic `Reject(code)`) in the call log

#### Scenario: Forbidden directive content is blocked before transport

- **WHEN** a supplied directive contains a coordinate, a UI path, a click sequence, an element locator, an action selection, a callback, or unresolved prose
- **THEN** the driver rejects it deterministically before any wire call and records the rejection in the call log

#### Scenario: No strategy inference

- **WHEN** only free-form goal prose is supplied without a directive (agent-authored or fixture)
- **THEN** the driver does not synthesize a strategy; it returns an explicit `DIRECTIVE_REQUIRED` result and records it

### Requirement: Scenario runner with three bounded entries

The harness SHALL implement three scenario entries mapping to the approved validation protocol: S1 Settings Exploration Depth 2, S2 Runtime Autonomous Exception Disposition, S3 Cross-Run Adaptation Simulation. Each entry SHALL record Input (Goal, strategy generation context, Directive), Runtime Execution evidence (Admission, lifecycle, terminal), Emulator observation actions, and Output (Validation Result); each entry SHALL bound the directive transport count exactly as the scenario defines.

#### Scenario: S1 — Settings exploration depth 2

- **WHEN** the S1 entry runs with DeclaredDepth 2 (approved D1 0/1/N semantics), container-expand / leaf-record-only exploration, no state mutation, and no boundary crossing
- **THEN** the result asserts the directive was accepted for exactly one Run; the Run progressed autonomously with zero Emulator calls after admission; record-only leaves produced no dispatched action; the Ledger (Tier A) is complete and deterministic; and terminal `Completed` is backed by `GoalEvidenceProduced` before `RunCompleted`

#### Scenario: S2 — Runtime autonomous exception disposition

- **WHEN** the S2 entry injects environment anomalies (unclassifiable node, popup, external boundary, unexpected navigation) after exactly one `run.strategy.start` call, and the Emulator performs no Run-internal control call from admission to terminal
- **THEN** the result asserts exactly one start call, zero Emulator intervention during the Run, and a terminal reached through the existing Agent-owned path under the disposition contract: outcome `PASS_RECOVERED` requires real recovery evidence (recovery lifecycle events, trap and recovery-state snapshot data) followed by continued execution, while outcome `PASS_BOUNDED_FAIL_CLOSED` requires a Runtime-originated terminal failure with an explicit FailureReason supported by EvidenceRef/lifecycle events, no unbounded retry, and no hidden fallback. A bounded fail-closed terminal MUST NOT be labeled a recovery success, and absent recovery evidence MUST NOT be fabricated as recovery events. The scenario result SHALL record the strategy-path recovery capability gap as `STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5` — this harness change proves the upper layer need not intervene in Run-internal exception disposition and does not purchase strategy-path recovery capability; a future buyer requiring recovery-and-continue for specific traps must establish a separate Runtime Recovery capability through OpenSpec and Human Gate, never via this harness.

#### Scenario: S3 — cross-run adaptation simulation

- **WHEN** the S3 entry completes Run 1, interprets Result 1 inside harness-local analysis, and authorizes Run 2 under a new `StrategyId` with an adaptation that references Result 1 facts
- **THEN** the result asserts two distinct one-Directive-one-Run executions, that Result 1 facts influenced only the Run 2 strategy (never any Runtime state or evidence), and that the future Memory insertion point is exactly `Historical Result → Strategy`, outside the Runtime boundary

### Requirement: Result collector truthfulness

The harness SHALL aggregate into one `ValidationResult` only facts that existing Runtime public surfaces expose: RunId, StrategyId, Admission outcome, lifecycle events, RunSnapshot fields (preserving their truth-source classification), trap data, `EvidenceRef`s resolved through `evidence.get`, and the terminal reason. The collector MUST NOT invent or compute a field the Runtime surface does not truthfully provide; any unavailable field SHALL be recorded explicitly as unavailable with its classification, never guessed.

#### Scenario: Result contains only runtime facts

- **WHEN** a `ValidationResult` is produced for a completed Run
- **THEN** every field traces to the admission response, the read-only wire surface, or the Tier-A in-process Agent public read model, and no field carries Emulator inference, Memory, or Plan content

#### Scenario: Unavailable surface data is marked, not fabricated

- **WHEN** a requested field has no truthful source on the current surface (for example full GoalEvidence or, on wire tiers, the Ledger)
- **THEN** the collector records the field as unavailable with its source classification and the report renders it as such

### Requirement: Evidence boundary verification

The harness SHALL verify, from the recorded call log, directive payloads, and the provenance of events/evidence, that the Emulator performed no Runtime state mutation, no FSM control, no action injection, and no evidence fabrication. Proof SHALL derive from existing surfaces only: zero mutating wire calls by construction, payload scans rejecting injected actions, all events belonging to the A/B source-classified vocabulary, and every `EvidenceRef` resolving through `evidence.get`. The harness MUST NOT instrument Runtime internals to obtain this proof.

#### Scenario: Boundary violations are detectable

- **WHEN** a directive payload contains injected action content, or a call record shows a mutating invocation, or an event/evidence reference cannot be attributed to runtime provenance
- **THEN** the boundary verifier flags the violation with the offending record and fails the gate

#### Scenario: Clean run proves the boundary

- **WHEN** a scenario completes with a legal call log and fully resolvable runtime-produced evidence
- **THEN** the verifier records positive bound evidence for all four prohibitions and the run does not fail

### Requirement: Tier-scoped coverage attestation

The harness SHALL scope its coverage attestation by environment tier: in Tier A (deterministic world, in-process) it MAY attest the complete `ExplorationLedgerView` via the existing public Agent read model (`CompileExplorationLedgerView`, a read-only evidence projection), and in Tier B/C (wire tiers) it SHALL report only those coverage facts truthfully available on the wire surface, marking the rest unavailable. The harness MUST NOT seek, and MUST NOT require, a new surface to obtain coverage on any tier.

#### Scenario: Tier A attests the full ledger

- **WHEN** a deterministic-world run completes and the harness host reads the Agent public read model in-process
- **THEN** the report includes discovered/visited/pending/unresolved/unknown-frontier counts with a stable digest, compiled only from existing evidence

#### Scenario: Wire tiers record coverage availability truthfully

- **WHEN** a real-emulator or real-device run completes over the wire
- **THEN** the report includes only the coverage facts the frozen wire surface exposes and explicitly marks ledger-level fields unavailable without claiming their absence is a runtime failure

### Requirement: Capability-test strategy, not script tests

The harness SHALL validate capabilities with tests following EvidenceFixture → Runtime Execution → Evidence Evaluation. Tests MUST NOT assert fixed click counts, fixed coordinates, fixed page text, fixed UI paths, or fixed action histories; runtime behavior is exercised, not scripted.

#### Scenario: Capability tests verify autonomy, not scripts

- **WHEN** the S1/S2/S3 validation entries are executed as tests
- **THEN** their assertions concern admission legality, autonomy (zero mid-run driver calls), evidence-backed terminals, ledger accounting (Tier A), and boundary cleanliness — never a fixed sequence of device interactions

### Requirement: Validation gates are enforceable

The harness SHALL express the four approved gates as checkable outcomes: G1 the driver can produce a legal Directive (deterministic validation); G2 the unmodified Runtime can be driven end-to-end for each scenario; G3 every Result field is Runtime-Evidence-backed; G4 boundary violations are detectable and reported. A scenario SHALL fail its gate when any of these outcomes is not satisfied, and SHALL NOT be repaired by weakening an assertion, modifying Runtime, or adding a surface.

#### Scenario: All gates pass on the deterministic tier

- **WHEN** the three scenarios complete on Tier A with legal directives, autonomous runs, evidence-backed results, and clean boundary records
- **THEN** all four gates report pass and the report is eligible for human adjudication

#### Scenario: A gate failure is reported, not masked

- **WHEN** any gate outcome is not satisfied
- **THEN** the scenario marks that gate failed with the offending evidence and stops without weakening checks, modifying Runtime, or inventing a surface