# uniagent-emulator-validation-harness

> OpenSpec change — Phase 2.5 UniAgent Emulator Validation Harness.

## Status

- **Phase**: APPLY (Human-authorized; S1/S3 implemented and green; S2 implemented under the revised Autonomous Exception Disposition semantics).
- **Classification**: Large Change (new project + new tooling boundary).
- **Authorized to create**: Validation Harness, Scenario Runner, Result Collector, Evidence Report.
- **Not part of this change**: Runtime modification, Agent/FSM/Traversal ownership, new wire/API, Memory, Planner, UniAgent.
- **S2 semantics (2026-08-26 Human revision)**: S2 = Runtime Autonomous Exception Disposition.
  Outcomes: PASS_RECOVERED (real recovery evidence + continued execution) or
  PASS_BOUNDED_FAIL_CLOSED (Runtime-originated failure, explicit reason, evidence-backed,
  no unbounded retry, no hidden fallback) — both with zero Emulator intervention.
  AUTONOMOUS_HANDLING != RECOVERY_ALWAYS_SUCCEEDS.
  Every S2 result records STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN /
  NOT_PURCHASED_BY_PHASE_2_5; a recovery-and-continue buyer needs a separate OpenSpec +
  Human Gate.

## What this is

A validation tool that drives the existing, unmodified RuntimeAgent from an external
abstract strategy loop (the Codex/dsh Agent Loop acting as UniAgent Emulator) and
produces evidence-backed validation results. It exists to answer: **"Can RuntimeAgent
already serve as a reliable execution substrate for a future UniAgent?"**

## What it is not

Not a UniAgent implementation. Not a Planner. Not Memory. Not a new Runtime capability.

## Boundaries

- Consumes only existing surfaces: `run.strategy.start` + the frozen read-only wire
  surface; Tier A additionally reads the in-process Agent public read model.
- Cannot mutate Runtime state, control FSM, inject actions, or fabricate evidence —
  and the Boundary Verifier proves that per run.

## Artifacts

| Artifact | Content |
|---|---|
| `proposal.md` | Why / What Changes / Capabilities / Impact |
| `design.md` | Decisions D1–D7, authority proof, stop-condition evaluation |
| `specs/uniagent-emulator-validation-harness/spec.md` | Normative requirements + scenarios |
| `tasks.md` | Bounded implementation steps + gates + graduation readiness |
| `.openspec.yaml` | Schema: `spec-driven` |

## Gates

- G1: Harness can produce a legal Directive.
- G2: Runtime can be driven end-to-end by the Emulator.
- G3: Result output is Runtime-Evidence-backed.
- G4: Boundary violations are detectable.

## References

- Strategy Contract: `openspec/changes/uniagent-runtimeagent-strategy-contract/`
- Ledger/depth: `openspec/changes/runtime-exploration-ledger-and-depth-control/`
- Runtime Contract: `docs/system/constitution/runtime-architecture-contract.md`