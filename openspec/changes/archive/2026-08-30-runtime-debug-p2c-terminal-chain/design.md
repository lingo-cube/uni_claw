# Design — runtime-debug-p2c-terminal-chain

## Context

P1b causal tree, P1d generator, P2a/b diffs are in place. The packet already stores TerminalState / LastGood / FirstBad and (for historical diagnosis packets) GapKind / Owner / Disposition / Confidence. P2c makes the terminal causal chain + stored diagnosis visible deterministically for the Agent's semantic judgment layer.

## Goals / Non-Goals

Goals: mechanical terminal chain view; stored-diagnosis projection explicitly marked STORED; empty-absent handling.

Non-Goals: computing GapKind/Owner/Disposition; merging across packets (P2b covers pairing); any repair/authority output.

## Decisions

### D1 — Projection, never computation
**Decision:** every diagnosis field is emitted only if present in the packet and grouped under `storedDiagnostics`; the note states diagnostics are STORED, never recomputed. Owner is trimmed to its scalar sub-fields.
**Why:** the Foundation FACT/INFERENCE/MISSING discipline — the tool surfaces facts; the Agent judges.

### D2 — Absent chain/diagnostics are honest empties
**Decision:** a generated structural packet yields `chain=[]` + `storedDiagnostics={}` with terminal still projected — no INSUFFICIENT_TRACE_COVERAGE (the command is a projection, not a chain requirement).
**Why:** distinguish "no data" (projected as empty) from "malformed" (reader fail-closed).

## Risks / Trade-offs

- [Stored diagnosis could be mistaken for current judgment] → STORED marker + note discipline.

## Migration Plan

None — additive command.

## Open Questions

None that would change the contract.
