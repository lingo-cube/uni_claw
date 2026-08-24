# semantic-evidence-fusion-baseline

**Status**: BASELINE (design only). **State**: decision + OpenSpec
proposal/design/spec/tasks/README defined. Pending APPLY gate
(`PROJECT_LEADER_APPLY_SEMANTIC_EVIDENCE_FUSION`).

## One-line

Freeze how SemanticEvidence enters the Runtime: the Evidence Fusion Boundary,
the sole consumer (Runtime Evidence Fusion), evidence → Fact conversion,
confidence usage (Evidence Weight, not Truth), freshness admission, and the
Trace/Fact relationship.

## Why

The Semantic Perception Contract defined the evidence shape and provider port.
This baseline defines the Runtime-side consumption boundary so Semantic stays an
Evidence Provider and Runtime stays the only Belief Authority.

## What changes

- **Boundary**: Observation → Perception Evidence → Evidence Fusion → Runtime
  Belief → Agent; never Semantic → Agent → Action.
- **Sole consumer**: Runtime Evidence Fusion only.
- **Fact conversion**: Semantic + Vision + Container History + Current Observation
  → Runtime Validation → Fact / Belief Update.
- **Confidence**: Evidence Weight only, never Truth.
- **Phase 1**: Container Identity Recovery; Semantic is an extra evidence
  provider, not a Resolver.
- **Fast/Slow**: synchronous bounded (empty failure) / async no-block checkpoint.
- **Freshness**: ObservationSequence / Timestamp / Scope checked; stale rejected.
- **Isolation**: Runtime depends only on ISemanticProvider.
- **Falsifiers**: F1–F10 frozen.

## Artifacts

- `docs/decisions/semantic-evidence-fusion-baseline.md`
- `proposal.md`
- `design.md`
- `specs/semantic-evidence-fusion-baseline/spec.md`
- `tasks.md`

## Validation

- `openspec validate semantic-evidence-fusion-baseline --type change --strict --no-interactive`
- `bash scripts/check-consistency.sh`
