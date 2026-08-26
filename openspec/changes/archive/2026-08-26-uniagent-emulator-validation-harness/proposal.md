## Why

Phase 2 Runtime Exploration is GRADUATED and the Phase 2.5 UniAgent Emulator Validation protocol is approved, but there is no repeatable validation entry: nothing currently proves that RuntimeAgent can be driven by an external abstract strategy loop rather than only by test scripts. This change creates the validation harness that makes the approved protocol executable against the existing, unmodified RuntimeAgent.

## What Changes

- Add a new validation-tooling project `src/UniClaw.Runtime.ValidationHarness` (registered in `src/UniClaw.Runtime.sln`) providing exactly five responsibilities:
  - **Emulator Driver** — accepts a human-readable Goal plus a `StrategyDirective` (authored by the existing Codex/dsh Agent Loop, or a recorded fixture in deterministic mode), strictly validates the directive (closed enums only, zero forbidden payload content), transports it via the existing `run.strategy.start`, and records an immutable call log. It never infers a strategy from prose.
  - **Scenario Runner** — three validation entries: S1 Settings Exploration Depth 2, S2 Runtime Autonomous Exception Disposition, S3 Cross-Run Adaptation Simulation.
  - **Result Collector** — aggregates only existing Runtime public evidence (`RunId`, `StrategyId`, Admission, Events, Snapshot, Trap, `EvidenceRef`s, Terminal Reason) into a `ValidationResult`; never invents a field the Runtime surface does not expose.
  - **Evidence Report** — renders the `ValidationResult` as JSON/Markdown, tier-scoped (Tier A may attest the Ledger via the existing in-process Agent public read model; wire tiers record unavailable fields as unavailable).
  - **Boundary Verifier** — proves from the call log + payloads + event/evidence provenance that the Emulator performed no Runtime state mutation, no FSM control, no action injection, and no evidence fabrication.
- Add a Tier-A in-process hosting composition that starts the existing DriverHost and coordinates `run.strategy.start` against deterministic fixture graphs supplied through an injected `RunGraphFactory`; the Emulator loop still connects over the loopback wire.
- Add capability tests under `tests/UniClaw.Runtime.Tests/ValidationHarness/` following EvidenceFixture → Runtime Execution → Evidence Evaluation; no fixed click counts, coordinates, page text, or UI paths.
- Freeze the four validation gates as spec-level behavior: G1 the harness can produce a legal Directive; G2 the unmodified Runtime can be driven end-to-end by the Emulator; G3 Result output is Runtime-Evidence-backed; G4 boundary violations are detectable.
- Do NOT change `UniClaw.Runtime`, `run.strategy.start`, any wire DTO, public protocol version, Phase 2 contract, ownership, lifecycle, completion authority, scenario knowledge, Phase 3 Memory, or Phase 4 dynamic depth. No Planner, no UniAgent, no Memory.

## Capabilities

### New Capabilities

- `uniagent-emulator-validation-harness`: Validation tooling that drives the existing, unmodified RuntimeAgent from an external abstract strategy loop and produces evidence-backed validation results, while remaining incapable of Runtime mutation, FSM control, action injection, or evidence fabrication.

### Modified Capabilities

- None. No spec-level behavior of any existing capability changes.

## Impact

- Production scope: NONE. `UniClaw.Runtime`, DriverHost transport, wire DTOs, and protocol versions are untouched; frozen `StrategyDirective` / `run.strategy.start` semantics remain byte-identical.
- New tooling scope: `src/UniClaw.Runtime.ValidationHarness/` (new project, added to the Runtime solution) with its own fixture environments and composition; capability tests and scenario tests under `tests/UniClaw.Runtime.Tests/ValidationHarness/`.
- Compatibility: purely additive tooling; no schema, wire, lifecycle, gate, or ownership change; the existing 18 active changes remain unchanged.
- Classification: **Large Change** (new project + new tooling boundary + new authority-guarded validation surface). Proposal/design/spec/tasks preparation is authorized by the Project Leader; apply to the repository requires a separate explicit Human Gate.
- Stop-condition guard: any discovered need for a new Runtime API, new wire contract, Phase 2 contract change, or ownership adjustment stops apply immediately — the harness must not self-extend into Runtime or Planning territory.