# Change: runtime-external-contract-baseline

> **Documentation-only contract baseline** for the Runtime ↔ External Intelligence
> Harness boundary. Recommended by `docs/decisions/runtime-dsh-architecture-gap-analysis.md`
> (NextGate = RUNTIME_EXTERNAL_CONTRACT_GATE).

## What it fixes

The Goal plane (`run.start`) and Data plane (8 read-only methods + snapshot/events/
evidence) are implemented but were never fixed as one unified External Contract.
This change freezes the implemented planes, defines the versioning policy, the
correlation/world-version primitives, and declares the three deferred planes
(Assistance / Guidance / Execution Handoff) as boundaries with authority
constraints — so later gates implement inside a defined contract frame instead of
growing ad-hoc protocols.

## Scope guardrails

- **Zero code**: no DTOs, no wire methods, no Runtime/plugin changes (F1).
- **No DSH into Runtime**: no DSH/Cordis types in the Runtime namespace (F2).
- **Deferred planes declared, not implemented**: zero-implementation evidence;
  boundaries + authority only, no wire freeze (F3/F9).
- **No future design assumed**: TaskSpec/AgentProfile/intelligence settings are
  named as non-existing (F7).
- **Frozen semantics preserved**: the 8 read-only methods + `run.start` keep exact
  current semantics (F4).
- **Authority unchanged**: DSH has no physical/GoalEvidence/binding/belief
  authority; Guidance ≠ Truth; Assistance = capability-gap expression (F5/F6).

## Documents

- `proposal.md` — buyer/gap/scope/falsifiers/authority
- `design.md` — five-plane contract + versioning + primitives + collaboration
  levels + authority clauses + implemented-surface mapping
- `specs/runtime-external-contract-baseline/spec.md` — R1–R10 requirements + scenarios
- `tasks.md` — slice checklist + falsifier mapping

## Next gate (planning context — does not buy the change)

After this baseline graduates: `RUNTIME_ASSISTANCE_SEAM_GATE` (L1 CONSULT, Plane 3
seam), then `DSH_ADAPTER_ALIGNMENT_GATE` (Integration Service layer), then
Guidance / Yield gates (L2/L3, far-term).
