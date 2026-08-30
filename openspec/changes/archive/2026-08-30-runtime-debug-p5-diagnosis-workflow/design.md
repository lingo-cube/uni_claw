# Design — runtime-debug-p5-diagnosis-workflow

## Context

P1a–P4c cover every read projection; P5 composes them into a one-pass diagnosis report and projects the implementation gate. The skill already hosts the P0 routing contracts; this change adds the toolchain route.

## Goals / Non-Goals

Goals: deterministic diagnose aggregation; evidence-gate projection; skill routing reference.

Non-Goals: executing repair; authoring FDP/Owner/GapKind/Disposition (Agent judgment); writing gate decisions into lifecycle authorities; any Runtime wiring.

## Decisions

### D1 — diagnose composes, never analyzes
**Decision:** `diagnose_workflow` only re-shapes Core outputs (compare/packet/tree/replay/minimize) into one report; it performs no new computation. Failed spans are collected recursively from the Core tree (the only structural detail it owns).
**Why:** keeps "one Core, thin surfaces" — the workflow is a surface, not a second analysis.

### D2 — Gate is projection with explicit ownership boundary
**Decision:** `evidence_gate` derives fdp/owner/evidence-present from stored facts; disposition EVIDENCE_COLLECTION requires FDP+refs and defers semantic GapKind/Owner to the Agent; the note declares it projection-never-authority.
**Why:** matches §12 (FDP/Owner/EvidenceRefs gate) without pretending semantic authority.

## Risks / Trade-offs

- [Gate could be mistaken for an authority] → explicit note + skill routing wording; no lifecycle writes.

## Migration Plan

None — additive command and skill reference.

## Open Questions

None that would change the contract; auto-repair and gate-to-lifecycle wiring are separate P6-era gates.
