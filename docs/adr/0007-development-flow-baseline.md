---
status: accepted
date: 2026-09-07
amended by: ADR-0029 (2026-09-26, gate semantics only: deterministic preflight, restricted closure C2 fastpath, owner-gate mechanical enforcement)
---

# Adopt the Development Flow baseline (8-state spine) and re-scope UniFlow to execution engine

The development process baseline is the Development Flow v2.2 spine —
UNDERSTAND → RESOLVE → PERSIST → PLAN → IMPLEMENT → REVIEW → VERIFY →
CLOSED — with an entry/resume PROTOCOL (not a state) at the head, an
optional LEARNING HOOK (not a state) at the tail, five failure edges,
and BLOCKED as an orthogonal disposition. UniFlow no longer defines the
lifecycle; it owns only HOW the flow executes: context economics (smart
zone ≈150K working value — past it, persist and split, never stretch the
session), Direct/Delegate routing, transient WorkItems, and model
routing. Durability is ALWAYS with variable depth: STANDARD+ changes
carry `changes/<id>/state.md` and support in-flight resume; MINIMAL is
commit-only and auto-upgrades to STANDARD the moment a change crosses a
session boundary. Verification declarations carry a level
(CONTRACT/DETERMINISTIC/SCENARIO/ENVIRONMENT — deliberately not E-numbered,
which belongs to the product's evidence semantics) and a four-part quad:
method, expected, actual, evidence ref. Supersedes ADR-0003 (Pre-UniFlow
boundary/plan persistence) and ADR-0004 (four semantic gates), whose
semantics are absorbed by Resolve Gate, PERSISTED/PLANNED, and the
failure edges. Structure is frozen; future evolution only through the
LEARN hook's evidence.

## Consequences

- ADR-0002 (WorkItem = transient delegation contract) unchanged.
- Skills resolve uncertainties or perform methods; they never own the flow.
- Concurrency fields and mechanized metrics are wait-for-buyer; no DAG,
  no ninth state.
