# FAST_CAPABILITY_DELIVERY_PROTOCOL_ADOPTION

> Status: Adopted as the default development protocol
> Date: 2026-08-09
> Role: Project Leader / Development Protocol Maintainer
> Scope: Process and governance only

## Decision

UniClaw adopts a two-lane development model:

```text
SEMANTIC_DISCOVERY_LANE       — NEW SEMANTICS → SLOW GOVERNANCE
CAPABILITY_DELIVERY_FAST_LANE — ACCEPTED SEMANTICS → FAST DELIVERY
```

The model formalizes the distinction between discovering what reality means and
delivering a capability whose semantics are already accepted. Existing Human
Gates, Semantic Gates, Architecture Gates, safety rules, frozen decisions, and
H4-3 auto-continue behavior remain valid and are normalized under this model.

## Lane A — Semantic Discovery Lane

Use this lane when reality, semantics, ownership, authority, dependency
direction, safety, completion, recovery, world-truth, or architecture invariants
may need to change. Its flow is:

```text
Evidence → Reality Distinction → Canonical Pressure → Reality Model
→ Validation / Admission → Capability Gap → Capability Candidate
→ Human / Semantic Gate → Architecture Challenge if needed
```

Discovery research may be parallelized. Semantic commitments remain serial and
belong to the Project Leader or the required Human Gate.

## Lane B — Capability Delivery Fast Lane

Use this lane only when the relevant CP and RM are accepted where required, the
capability gap is established, the capability semantics are approved/frozen, and
the work fits the existing architecture invariants.

The default loop is:

```text
Accepted Semantic Need → Minimum Falsifying Scenario → Architecture Fit Check
→ Minimum Implementation → Executable Verification → Diagnose → Repair
→ Re-run → Freeze
```

The Architecture Fit Check asks only whether mutable-state ownership, decision
authority, dependency direction, architecture invariants, safety authority, and
external-world authority remain unchanged. If all remain unchanged, record
`ARCHITECTURE_FIT_CONFIRMED`; no standalone Architecture Challenge is required.

## Default Continuation

Inside the accepted semantic envelope, `AUTO_CONTINUE` is the default. The
Project Leader continues automatically after ordinary worker results, test
failures, bounded repairs, repeated validation, documentation reconciliation,
and local implementation decisions.

Auto-continue applies to mechanical failures, test-fixture failures, local
behavior/composition gaps, purchased-semantic assertion mismatches,
documentation reconciliation, local build failures, repairable implementation
regressions, style/lint/static failures, and bounded deterministic test failures.

Workers return evidence; they do not imply Fast Lane completion. The Project
Leader owns continuation and final validation.

## Canonical Hard Gates

| Gate | Trigger | Result |
|---|---|---|
| `HG-SEMANTIC` | Accepted reality is contradicted or semantics must expand | `NEW_SEMANTIC_PRESSURE`, `NEW_REALITY_MODEL_REQUIRED`, or `SEMANTIC_GATE_REQUIRED` |
| `HG-ARCHITECTURE` | Layer, ownership, authority, dependency direction, or invariant must change | `ARCHITECTURE_GATE_REQUIRED` |
| `HG-SAFETY` | Safety authorization or irreversible-action semantics must change | `SAFETY_SEMANTIC_GATE_REQUIRED` |
| `HG-HUMAN` | Repository governance reserves the next boundary decision for Human authorization | `HUMAN_GATE_REQUIRED` |
| `HG-VALIDATION` | Required validation is unsatisfied or cannot distinguish semantics mechanically | `VALIDATION_BLOCKED` |
| `HG-SCOPE` | The smallest correct implementation exceeds authorization | `AUTHORIZED_SCOPE_EXCEEDED` |

On a Hard Gate, preserve executable evidence, record the exact failed
assumption, exit the Fast Lane, resolve only that pressure in Semantic
Discovery, and return to the same Fast Lane afterward. Semantic uncertainty may
flow upward; it must never be silently normalized into implementation.

## Human Role

Human authorization is primarily required for real semantic commitments,
architecture boundary changes, ownership/authority changes, safety-semantic
changes, and explicitly reserved governance decisions. Human relay is not
required for ordinary worker dispatch, local implementation choices, test repair,
repeated validator runs, or documentation reconciliation already covered by the
authorization envelope.

## Project Leader Role

The Project Leader selects the lane, preserves the accepted semantic envelope,
dispatches workers, integrates evidence, owns auto-continuation, detects Hard
Gates, maintains repository truth, and completes validation. Final semantic and
architecture authority is not delegable to workers.

## Non-Normative Worked Example

The following illustrates the protocol only; it does not authorize CP-12 Runtime
implementation:

```text
CP-12 / RM-10 / GC-03 already accepted
→ Capability Delivery Fast Lane
→ minimum Wi-Fi vs Wi-Fi Calling falsifying Scenario
→ Architecture Fit Check
→ implement / test / repair
→ stop only if ownership, invariant, safety, or semantic boundary changes
```

The example is non-normative and does not create a CP-12 task, capability
candidate, OpenSpec authorization, or Runtime implementation permission.

## Compatibility and Scope

This adoption preserves existing Human Gates and semantic protocols. It
generalizes H4-3 auto-continue for bounded accepted-capability delivery while
retaining mandatory stops for semantic conflict, architecture pressure, safety
changes, Human authorization, validation blocks, routing failure, scope
expansion, and repository-state conflict.

```text
Development Model: TWO_LANE
Semantic Discovery Lane: ADOPTED
Capability Delivery Fast Lane: ADOPTED
Default Fast-Lane Behavior: AUTO_CONTINUE
Human Role: BOUNDARY_DECISIONS_ONLY
Project Leader Role: AUTONOMOUS_BOUNDED_EXECUTION
Runtime Changes: NONE
Architecture Changes: NONE
Semantic Changes: PROCESS_ONLY
```

## Stop

This decision adopts the process only. CP-12 remains unimplemented and no
Runtime behavior, new CP, RM, capability, or architecture change is authorized
by this artifact.
